ImplFile
  (ParsedImplFileInput
     ("/root/Type/With 04.fs", false, QualifiedNameOfFile Module, [],
      [SynModuleOrNamespace
         ([Module], false, NamedModule,
          [Types
             ([SynTypeDefn
                 (SynComponentInfo
                    ([], None, [], [U],
                     PreXmlDoc ((3,0), FSharp.Compiler.Xml.XmlDocCollector),
                     false, None, (3,5--3,6)),
                  Simple
                    (Union
                       (None,
                        [SynUnionCase
                           ([], SynIdent (A, None), Fields [],
                            PreXmlDoc ((4,4), FSharp.Compiler.Xml.XmlDocCollector),
                            None, (4,6--4,7), { BarRange = Some (4,4--4,5) })],
                        (4,4--4,7)), (4,4--4,7)),
                  [Member
                     (SynBinding
                        (None, Normal, false, false, [],
                         PreXmlDoc ((6,4), FSharp.Compiler.Xml.XmlDocCollector),
                         SynValData
                           (Some { IsInstance = true
                                   IsDispatchSlot = false
                                   IsOverrideOrExplicitImpl = false
                                   IsFinal = false
                                   GetterOrSetterIsCompilerGenerated = false
                                   MemberKind = Member },
                            SynValInfo
                              ([[SynArgInfo ([], false, None)]; []],
                               SynArgInfo ([], false, None)), None),
                         LongIdent
                           (SynLongIdent
                              ([this; P], [(6,15--6,16)], [None; None]), None,
                            None, Pats [], None, (6,11--6,17)), None,
                         Const (Int32 1, (6,20--6,21)), (6,11--6,17),
                         NoneAtInvisible, { LeadingKeyword = Member (6,4--6,10)
                                            InlineKeyword = None
                                            EqualsRange = Some (6,18--6,19) }),
                      (6,4--6,21))], None, (3,5--6,21),
                  { LeadingKeyword = Type (3,0--3,4)
                    EqualsRange = Some (3,7--3,8)
                    WithKeyword = Some (4,8--4,12) })], (3,0--6,21));
           Expr (Const (Int32 2, (8,0--8,1)), (8,0--8,1))],
          PreXmlDoc ((1,0), FSharp.Compiler.Xml.XmlDocCollector), [], None,
          (1,0--8,1), { LeadingKeyword = Module (1,0--1,6) })], (true, true),
      { ConditionalDirectives = []
        WarnDirectives = []
        CodeComments = [] }, set []))
