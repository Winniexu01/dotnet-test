ImplFile
  (ParsedImplFileInput
     ("/root/ModuleOrNamespace/Nested module 11.fs", false,
      QualifiedNameOfFile Module, [],
      [SynModuleOrNamespace
         ([Module], false, NamedModule,
          [NestedModule
             (SynComponentInfo
                ([], None, [], [],
                 PreXmlDoc ((3,0), FSharp.Compiler.Xml.XmlDocCollector), false,
                 None, (3,0--5,0)), false, [], false, (3,0--5,0),
              { ModuleKeyword = Some (3,0--3,6)
                EqualsRange = None }); Expr (Ident A, (5,0--5,1))],
          PreXmlDoc ((1,0), FSharp.Compiler.Xml.XmlDocCollector), [], None,
          (1,0--5,1), { LeadingKeyword = Module (1,0--1,6) })], (true, true),
      { ConditionalDirectives = []
        WarnDirectives = []
        CodeComments = [] }, set []))

(3,7)-(5,0) parse error Incomplete structured construct at or before this point in definition. Expected '=' or other token.
