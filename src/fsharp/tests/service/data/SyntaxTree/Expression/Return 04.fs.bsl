ImplFile
  (ParsedImplFileInput
     ("/root/Expression/Return 04.fs", false, QualifiedNameOfFile Module, [],
      [SynModuleOrNamespace
         ([Module], false, NamedModule,
          [Let
             (false,
              [SynBinding
                 (None, Normal, false, false, [],
                  PreXmlDoc ((3,0), FSharp.Compiler.Xml.XmlDocCollector),
                  SynValData
                    (None, SynValInfo ([], SynArgInfo ([], false, None)), None),
                  Wild (3,4--3,5), None,
                  App
                    (NonAtomic, false, Ident async,
                     ComputationExpr
                       (false,
                        YieldOrReturn
                          ((false, true),
                           Paren
                             (Upcast
                                (New
                                   (false,
                                    LongIdent
                                      (SynLongIdent ([MyType], [], [None])),
                                    Const (Unit, (5,26--5,28)), (5,16--5,28)),
                                 LongIdent
                                   (SynLongIdent ([IDisposable], [], [None])),
                                 (5,16--5,43)), (5,15--5,16), Some (5,43--5,44),
                              (5,15--5,44)), (5,8--5,44),
                           { YieldOrReturnKeyword = (5,8--5,14) }), (4,10--6,5)),
                     (4,4--6,5)), (3,4--3,5), NoneAtLet,
                  { LeadingKeyword = Let (3,0--3,3)
                    InlineKeyword = None
                    EqualsRange = Some (3,6--3,7) })], (3,0--6,5))],
          PreXmlDoc ((1,0), FSharp.Compiler.Xml.XmlDocCollector), [], None,
          (1,0--6,5), { LeadingKeyword = Module (1,0--1,6) })], (true, true),
      { ConditionalDirectives = []
        WarnDirectives = []
        CodeComments = [] }, set []))
