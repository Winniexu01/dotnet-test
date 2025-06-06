// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

/// Primary relations on types and signatures, with the exception of
/// constraint solving and method overload resolution.
module internal FSharp.Compiler.TypeRelations

open FSharp.Compiler.Features
open Internal.Utilities.Collections
open Internal.Utilities.Library

open FSharp.Compiler.DiagnosticsLogger
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeBasics
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.TypeHierarchy

open Import

#nowarn "3391"

/// Implements a :> b without coercion based on finalized (no type variable) types
// Note: This relation is approximate and not part of the language specification.
//
//  Some appropriate uses:
//     patcompile.fs: IsDiscrimSubsumedBy (approximate warning for redundancy of 'isinst' patterns)
//     tc.fs: TcRuntimeTypeTest (approximate warning for redundant runtime type tests)
//     tc.fs: TcExnDefnCore (error for bad exception abbreviation)
//     ilxgen.fs: GenCoerce (omit unnecessary castclass or isinst instruction)
//
let rec TypeDefinitelySubsumesTypeNoCoercion ndeep g amap m ty1 ty2 =

    if ndeep > 100 then
        error(InternalError("Large class hierarchy (possibly recursive, detected in TypeDefinitelySubsumesTypeNoCoercion), ty1 = " + (DebugPrint.showType ty1), m))

    if ty1 === ty2 then true
    elif typeEquiv g ty1 ty2 then true
    else
        let ty1 = stripTyEqns g ty1
        let ty2 = stripTyEqns g ty2
        // F# reference types are subtypes of type 'obj'
        (typeEquiv g ty1 g.obj_ty_ambivalent && isRefTy g ty2) ||
        // Follow the supertype chain
        (isAppTy g ty2 &&
        isRefTy g ty2 &&

        ((match GetSuperTypeOfType g amap m ty2 with
            | None -> false
            | Some ty -> TypeDefinitelySubsumesTypeNoCoercion (ndeep+1) g amap m ty1 ty) ||

        // Follow the interface hierarchy
        (isInterfaceTy g ty1 &&
            ty2 |> GetImmediateInterfacesOfType SkipUnrefInterfaces.Yes g amap m
                |> List.exists (TypeDefinitelySubsumesTypeNoCoercion (ndeep+1) g amap m ty1))))

let stripAll stripMeasures g ty =
    if stripMeasures then
        ty |> stripTyEqnsWrtErasure EraseAll g |> stripMeasuresFromTy g
    else
        ty |> stripTyEqns g

/// The feasible equivalence relation. Part of the language spec.
let rec TypesFeasiblyEquivalent stripMeasures ndeep g amap m ty1 ty2 =

    if ndeep > 100 then
        error(InternalError("Large class hierarchy (possibly recursive, detected in TypeFeasiblySubsumesType), ty1 = " + (DebugPrint.showType ty1), m));

    let ty1 = stripAll stripMeasures g ty1
    let ty2 = stripAll stripMeasures g ty2

    match ty1, ty2 with
    | TType_measure _, TType_measure _
    | TType_var _, _
    | _, TType_var _ -> true

    | TType_app (tcref1, l1, _), TType_app (tcref2, l2, _) when tyconRefEq g tcref1 tcref2 ->
        List.lengthsEqAndForall2 (TypesFeasiblyEquivalent stripMeasures ndeep g amap m) l1 l2

    | TType_anon (anonInfo1, l1),TType_anon (anonInfo2, l2) ->
        (evalTupInfoIsStruct anonInfo1.TupInfo = evalTupInfoIsStruct anonInfo2.TupInfo) &&
        (match anonInfo1.Assembly, anonInfo2.Assembly with ccu1, ccu2 -> ccuEq ccu1 ccu2) &&
        (anonInfo1.SortedNames = anonInfo2.SortedNames) &&
        List.lengthsEqAndForall2 (TypesFeasiblyEquivalent stripMeasures ndeep g amap m) l1 l2

    | TType_tuple (tupInfo1, l1), TType_tuple (tupInfo2, l2) ->
        evalTupInfoIsStruct tupInfo1 = evalTupInfoIsStruct tupInfo2 &&
        List.lengthsEqAndForall2 (TypesFeasiblyEquivalent stripMeasures ndeep g amap m) l1 l2

    | TType_fun (domainTy1, rangeTy1, _), TType_fun (domainTy2, rangeTy2, _) ->
        TypesFeasiblyEquivalent stripMeasures ndeep g amap m domainTy1 domainTy2 &&
        TypesFeasiblyEquivalent stripMeasures ndeep g amap m rangeTy1 rangeTy2

    | _ ->
        false

/// The feasible equivalence relation. Part of the language spec.
let TypesFeasiblyEquiv ndeep g amap m ty1 ty2 =
    TypesFeasiblyEquivalent false ndeep g amap m ty1 ty2

/// The feasible equivalence relation after stripping Measures.
let TypesFeasiblyEquivStripMeasures g amap m ty1 ty2 =
    TypesFeasiblyEquivalent true 0 g amap m ty1 ty2

let inline TryGetCachedTypeSubsumption (g: TcGlobals) (amap: ImportMap) key =
    if g.langVersion.SupportsFeature LanguageFeature.UseTypeSubsumptionCache then
        match amap.TypeSubsumptionCache.TryGetValue(key) with
        | true, subsumes ->
            ValueSome subsumes
        | false, _ ->
            ValueNone
    else
        ValueNone

let inline UpdateCachedTypeSubsumption (g: TcGlobals) (amap: ImportMap) key subsumes : unit =
    if g.langVersion.SupportsFeature LanguageFeature.UseTypeSubsumptionCache then
        amap.TypeSubsumptionCache.TryAdd(key, subsumes) |> ignore

/// The feasible coercion relation. Part of the language spec.
let rec TypeFeasiblySubsumesType ndeep (g: TcGlobals) (amap: ImportMap) m (ty1: TType) (canCoerce: CanCoerce) (ty2: TType) =

    if ndeep > 100 then
        error(InternalError("Large class hierarchy (possibly recursive, detected in TypeFeasiblySubsumesType), ty1 = " + (DebugPrint.showType ty1), m))

    let ty1 = stripTyEqns g ty1
    let ty2 = stripTyEqns g ty2

    // Check if language feature supported
    let key = TTypeCacheKey.FromStrippedTypes (ty1, ty2, canCoerce)

    match TryGetCachedTypeSubsumption g amap key with
    | ValueSome subsumes ->
        subsumes
    | ValueNone ->
        let subsumes =
            match ty1, ty2 with
            | TType_measure _, TType_measure _
            | TType_var _, _ | _, TType_var _ ->
                true

            | TType_app (tc1, l1, _), TType_app (tc2, l2, _) when tyconRefEq g tc1 tc2 ->
                List.lengthsEqAndForall2 (TypesFeasiblyEquiv ndeep g amap m) l1 l2

            | TType_tuple _, TType_tuple _
            | TType_anon _, TType_anon _
            | TType_fun _, TType_fun _ ->
                TypesFeasiblyEquiv ndeep g amap m ty1 ty2

            | _ ->
                // F# reference types are subtypes of type 'obj'
                    if isObjTyAnyNullness g ty1 && (canCoerce = CanCoerce || isRefTy g ty2) then
                        true
                    elif isAppTy g ty2 && (canCoerce = CanCoerce || isRefTy g ty2) && TypeFeasiblySubsumesTypeWithSupertypeCheck g amap m ndeep ty1 ty2 then
                        true
                    else
                        let interfaces = GetImmediateInterfacesOfType SkipUnrefInterfaces.Yes g amap m ty2
                        // See if any interface in type hierarchy of ty2 is a supertype of ty1
                        List.exists (TypeFeasiblySubsumesType (ndeep + 1) g amap m ty1 NoCoerce) interfaces

        UpdateCachedTypeSubsumption g amap key subsumes

        subsumes

and TypeFeasiblySubsumesTypeWithSupertypeCheck g amap m ndeep ty1 ty2 =
    match GetSuperTypeOfType g amap m ty2 with
    | None -> false
    | Some ty -> TypeFeasiblySubsumesType (ndeep + 1) g amap m ty1 NoCoerce ty

/// Choose solutions for Expr.TyChoose type "hidden" variables introduced
/// by letrec nodes. Also used by the pattern match compiler to choose type
/// variables when compiling patterns at generalized bindings.
///     e.g. let ([], x) = ([], [])
/// Here x gets a generalized type "list<'T>".
let ChooseTyparSolutionAndRange (g: TcGlobals) amap (tp:Typar) =
    let m = tp.Range
    let (maxTy, isRefined), m =
         let initialTy =
             match tp.Kind with
             | TyparKind.Type -> g.obj_ty_noNulls
             | TyparKind.Measure -> TType_measure(Measure.One m)
         // Loop through the constraints computing the lub
         (((initialTy, false), m), tp.Constraints) ||> List.fold (fun ((maxTy, isRefined), _) tpc ->
             let join m x =
                 if TypeFeasiblySubsumesType 0 g amap m x CanCoerce maxTy then maxTy, isRefined
                 elif TypeFeasiblySubsumesType 0 g amap m maxTy CanCoerce x then x, true
                 else errorR(Error(FSComp.SR.typrelCannotResolveImplicitGenericInstantiation((DebugPrint.showType x), (DebugPrint.showType maxTy)), m)); maxTy, isRefined
             // Don't continue if an error occurred and we set the value eagerly
             if tp.IsSolved then (maxTy, isRefined), m else
             match tpc with
             | TyparConstraint.CoercesTo(x, m) ->
                 join m x, m
             | TyparConstraint.SimpleChoice(_, m) -> 
                 errorR(Error(FSComp.SR.typrelCannotResolveAmbiguityInPrintf(), m))
                 (maxTy, isRefined), m
             | TyparConstraint.SupportsNull m ->
                 ((addNullnessToTy KnownWithNull maxTy), isRefined), m
             | TyparConstraint.SupportsComparison m -> 
                 join m g.mk_IComparable_ty, m
             | TyparConstraint.IsEnum(_, m) -> 
                 errorR(Error(FSComp.SR.typrelCannotResolveAmbiguityInEnum(), m))
                 (maxTy, isRefined), m
             | TyparConstraint.IsDelegate(_, _, m) ->
                 errorR(Error(FSComp.SR.typrelCannotResolveAmbiguityInDelegate(), m))
                 (maxTy, isRefined), m
             | TyparConstraint.IsNonNullableStruct m ->
                 join m g.int_ty, m
             | TyparConstraint.IsUnmanaged m ->
                 errorR(Error(FSComp.SR.typrelCannotResolveAmbiguityInUnmanaged(), m))
                 (maxTy, isRefined), m
             | TyparConstraint.NotSupportsNull m // NOTE: this doesn't "force" non-nullness, since it is the default choice in 'obj' or 'int'
             | TyparConstraint.SupportsEquality m
             | TyparConstraint.AllowsRefStruct m
             | TyparConstraint.RequiresDefaultConstructor m
             | TyparConstraint.IsReferenceType m
             | TyparConstraint.MayResolveMember(_, m)
             | TyparConstraint.DefaultsTo(_,_, m) ->
                 (maxTy, isRefined), m
             )

    if g.langVersion.SupportsFeature LanguageFeature.DiagnosticForObjInference then
        match tp.Kind with
        | TyparKind.Type ->
            if not isRefined then
                informationalWarning(Error(FSComp.SR.typrelNeverRefinedAwayFromTop(), m))
        | TyparKind.Measure -> ()

    maxTy, m

let ChooseTyparSolution g amap tp =
    let ty, m = ChooseTyparSolutionAndRange g amap tp
    if tp.Rigidity = TyparRigidity.Anon && typeEquiv g ty (TType_measure(Measure.One m)) then
        warning(Error(FSComp.SR.csCodeLessGeneric(), tp.Range))
    ty

// Solutions can, in theory, refer to each other
// For example
//   'a = Expr<'b>
//   'b = int
// In this case the solutions are
//   'a = Expr<int>
//   'b = int
// We ground out the solutions by repeatedly instantiating
let IterativelySubstituteTyparSolutions g tps solutions =
    let tpenv = mkTyparInst tps solutions
    let rec loop n curr =
        let curr' = curr |> instTypes tpenv
        // We cut out at n > 40 just in case this loops. It shouldn't, since there should be no cycles in the
        // solution equations, and we've only ever seen one example where even n = 2 was required.
        // Perhaps it's possible in error recovery some strange situations could occur where cycles
        // arise, so it's better to be on the safe side.
        //
        // We don't give an error if we hit the limit since it's feasible that the solutions of unknowns
        // is not actually relevant to the rest of type checking or compilation.
        if n > 40 || List.forall2 (typeEquiv g) curr curr' then
            curr
        else
            loop (n+1) curr'

    loop 0 solutions

let ChooseTyparSolutionsForFreeChoiceTypars g amap e =
    match stripDebugPoints e with
    | Expr.TyChoose (tps, e1, _m)  ->

        /// Only make choices for variables that are actually used in the expression
        let ftvs = (freeInExpr CollectTyparsNoCaching e1).FreeTyvars.FreeTypars
        let tps = tps |> List.filter (Zset.memberOf ftvs)

        let solutions =  tps |> List.map (ChooseTyparSolution g amap) |> IterativelySubstituteTyparSolutions g tps

        let tpenv = mkTyparInst tps solutions

        instExpr g tpenv e1

    | _ -> e

/// Break apart lambdas according to a given expected ValReprInfo that the lambda implements.
/// Needs ChooseTyparSolutionsForFreeChoiceTypars because it's used in
/// PostTypeCheckSemanticChecks before we've eliminated these nodes.
let tryDestLambdaWithValReprInfo g amap valReprInfo (lambdaExpr, ty) =
    let (ValReprInfo (tpNames, _, _)) = valReprInfo
    let rec stripLambdaUpto n (e, ty) =
        match stripDebugPoints e with
        | Expr.Lambda (_, None, None, v, b, _, retTy) when n > 0 ->
            let vs', b', retTy' = stripLambdaUpto (n-1) (b, retTy)
            (v :: vs', b', retTy')
        | _ ->
            ([], e, ty)

    let rec startStripLambdaUpto n (e, ty) =
        match stripDebugPoints e with
        | Expr.Lambda (_, ctorThisValOpt, baseValOpt, v, b, _, retTy) when n > 0 ->
            let vs', b', retTy' = stripLambdaUpto (n-1) (b, retTy)
            (ctorThisValOpt, baseValOpt, (v :: vs'), b', retTy')
        | Expr.TyChoose (_tps, _b, _) ->
            startStripLambdaUpto n (ChooseTyparSolutionsForFreeChoiceTypars g amap e, ty)
        | _ ->
            (None, None, [], e, ty)

    let n = valReprInfo.NumCurriedArgs

    let tps, bodyExpr, bodyTy =
        match stripDebugPoints lambdaExpr with
        | Expr.TyLambda (_, tps, b, _, retTy) when not (isNil tpNames) -> tps, b, retTy
        | _ -> [], lambdaExpr, ty

    let ctorThisValOpt, baseValOpt, vsl, body, retTy = startStripLambdaUpto n (bodyExpr, bodyTy)

    if vsl.Length <> n then
        None
    else
        Some (tps, ctorThisValOpt, baseValOpt, vsl, body, retTy)

let destLambdaWithValReprInfo g amap valReprInfo (lambdaExpr, ty) =
    match tryDestLambdaWithValReprInfo g amap valReprInfo (lambdaExpr, ty) with
    | None -> error(Error(FSComp.SR.typrelInvalidValue(), lambdaExpr.Range))
    | Some res -> res

let IteratedAdjustArityOfLambdaBody g arities vsl body  =
      (arities, vsl, ([], body)) |||> List.foldBack2 (fun arities vs (allvs, body) ->
          let vs, body = AdjustArityOfLambdaBody g arities vs body
          vs :: allvs, body)

/// Do IteratedAdjustArityOfLambdaBody for a series of iterated lambdas, producing one method.
/// The required iterated function arity (List.length valReprInfo) must be identical
/// to the iterated function arity of the input lambda (List.length vsl)
let IteratedAdjustLambdaToMatchValReprInfo g amap valReprInfo lambdaExpr =

    let lambdaExprTy = tyOfExpr g lambdaExpr

    let tps, ctorThisValOpt, baseValOpt, vsl, body, bodyTy = destLambdaWithValReprInfo g amap valReprInfo (lambdaExpr, lambdaExprTy)

    let arities = valReprInfo.AritiesOfArgs

    if arities.Length <> vsl.Length then
        errorR(InternalError(sprintf "IteratedAdjustLambdaToMatchValReprInfo, #arities = %d, #vsl = %d" arities.Length vsl.Length, body.Range))

    let vsl, body = IteratedAdjustArityOfLambdaBody g arities vsl body

    tps, ctorThisValOpt, baseValOpt, vsl, body, bodyTy

/// "Single Feasible Type" inference
/// Look for the unique supertype of ty2 for which ty2 :> ty1 might feasibly hold
let FindUniqueFeasibleSupertype g amap m ty1 ty2 =
    let n2 = nullnessOfTy g ty2
    let nullify t = addNullnessToTy n2 t

    let supertypes = 
        Option.toList (GetSuperTypeOfType g amap m ty2) @ 
        (GetImmediateInterfacesOfType SkipUnrefInterfaces.Yes g amap m ty2)

    supertypes 
    |> List.tryFind (TypeFeasiblySubsumesType 0 g amap m ty1 NoCoerce)
    |> Option.map nullify
