ImplFile
  (ParsedImplFileInput
     ("/root/Type/SynTypeDefnWithUnionContainsTheRangeOfTheEqualsSign.fs", false,
      QualifiedNameOfFile SynTypeDefnWithUnionContainsTheRangeOfTheEqualsSign,
      [],
      [SynModuleOrNamespace
         ([SynTypeDefnWithUnionContainsTheRangeOfTheEqualsSign], false,
          AnonModule,
          [Types
             ([SynTypeDefn
                 (SynComponentInfo
                    ([], None, [], [Shape],
                     PreXmlDoc ((2,0), FSharp.Compiler.Xml.XmlDocCollector),
                     false, None, (2,5--2,10)),
                  Simple
                    (Union
                       (None,
                        [SynUnionCase
                           ([], SynIdent (Square, None),
                            Fields
                              [SynField
                                 ([], false, None,
                                  LongIdent (SynLongIdent ([int], [], [None])),
                                  false,
                                  PreXmlDoc ((3,16), FSharp.Compiler.Xml.XmlDocCollector),
                                  None, (3,16--3,19), { LeadingKeyword = None
                                                        MutableKeyword = None })],
                            PreXmlDoc ((3,4), FSharp.Compiler.Xml.XmlDocCollector),
                            None, (3,6--3,19), { BarRange = Some (3,4--3,5) });
                         SynUnionCase
                           ([], SynIdent (Rectangle, None),
                            Fields
                              [SynField
                                 ([], false, None,
                                  LongIdent (SynLongIdent ([int], [], [None])),
                                  false,
                                  PreXmlDoc ((4,19), FSharp.Compiler.Xml.XmlDocCollector),
                                  None, (4,19--4,22), { LeadingKeyword = None
                                                        MutableKeyword = None });
                               SynField
                                 ([], false, None,
                                  LongIdent (SynLongIdent ([int], [], [None])),
                                  false,
                                  PreXmlDoc ((4,25), FSharp.Compiler.Xml.XmlDocCollector),
                                  None, (4,25--4,28), { LeadingKeyword = None
                                                        MutableKeyword = None })],
                            PreXmlDoc ((4,4), FSharp.Compiler.Xml.XmlDocCollector),
                            None, (4,6--4,28), { BarRange = Some (4,4--4,5) })],
                        (3,4--4,28)), (3,4--4,28)), [], None, (2,5--4,28),
                  { LeadingKeyword = Type (2,0--2,4)
                    EqualsRange = Some (2,11--2,12)
                    WithKeyword = None })], (2,0--4,28))], PreXmlDocEmpty, [],
          None, (2,0--5,0), { LeadingKeyword = None })], (true, true),
      { ConditionalDirectives = []
        WarnDirectives = []
        CodeComments = [] }, set []))
