﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Collections
Imports Microsoft.CodeAnalysis.EditAndContinue
Imports Microsoft.CodeAnalysis.EditAndContinue.UnitTests
Imports Microsoft.CodeAnalysis.Emit
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic.EditAndContinue.UnitTests
    <UseExportProvider>
    Public Class StatementEditingTests
        Inherits EditingTestBase
#Region "Misc"
        <Fact>
        Public Sub VariableDeclaration_Insert()
            Dim src1 = "If x = 1 : x += 1 : End If"
            Dim src2 = "Dim x = 1 : If x = 1 : x += 1 : End If"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [Dim x = 1]@8",
                "Insert [x = 1]@12",
                "Insert [x]@12")
        End Sub

        <Fact>
        Public Sub VariableDeclaration_Update()
            Dim src1 = "Dim x = F(1), y = G(2)"
            Dim src2 = "Dim x = F(3), y = G(4)"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [x = F(1)]@12 -> [x = F(3)]@12",
                "Update [y = G(2)]@22 -> [y = G(4)]@22")
        End Sub

        <Fact>
        Public Sub Redim1()
            Dim src1 = "ReDim Preserve a(F(Function() 1), 10, 20)"
            Dim src2 = "ReDim a(F(Function() 2), 1, 2)"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [ReDim Preserve a(F(Function() 1), 10, 20)]@8 -> [ReDim a(F(Function() 2), 1, 2)]@8",
                "Update [a(F(Function() 1), 10, 20)]@23 -> [a(F(Function() 2), 1, 2)]@14",
                "Update [Function() 1]@27 -> [Function() 2]@18")
        End Sub

        <Fact>
        Public Sub Assignments()
            Dim src1 = "a = F(Function() 1) : " &
                       "a += F(Function() 2) : " &
                       "a -= F(Function() 3) : " &
                       "a *= F(Function() 4) : " &
                       "a /= F(Function() 5) : " &
                       "a \= F(Function() 6) : " &
                       "a ^= F(Function() 7) : " &
                       "a <<= F(Function() 8) : " &
                       "a >>= F(Function() 9) : " &
                       "a &= F(Function() 10) : " &
                       "Mid(s, F(Function() 11), 1) = F(Function() ""a"")"

            Dim src2 = "a = F(Function() 100) : " &
                       "a += F(Function() 200) : " &
                       "a -= F(Function() 300) : " &
                       "a *= F(Function() 400) : " &
                       "a /= F(Function() 500) : " &
                       "a \= F(Function() 600) : " &
                       "a ^= F(Function() 700) : " &
                       "a <<= F(Function() 800) : " &
                       "a >>= F(Function() 900) : " &
                       "a &= F(Function() 1000) : " &
                       "Mid(s, F(Function() 1100), 1) = F(Function() ""b"")"

            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [Function() 1]@14 -> [Function() 100]@14",
                "Update [Function() 2]@37 -> [Function() 200]@39",
                "Update [Function() 3]@60 -> [Function() 300]@64",
                "Update [Function() 4]@83 -> [Function() 400]@89",
                "Update [Function() 5]@106 -> [Function() 500]@114",
                "Update [Function() 6]@129 -> [Function() 600]@139",
                "Update [Function() 7]@152 -> [Function() 700]@164",
                "Update [Function() 8]@176 -> [Function() 800]@190",
                "Update [Function() 9]@200 -> [Function() 900]@216",
                "Update [Function() 10]@223 -> [Function() 1000]@241",
                "Update [Function() 11]@249 -> [Function() 1100]@269",
                "Update [Function() ""a""]@272 -> [Function() ""b""]@294")
        End Sub

        <Fact>
        Public Sub EventStatements()
            Dim src1 = "AddHandler e, Function(f) f : RemoveHandler e, Function(f) f : RaiseEvent e()"
            Dim src2 = "RemoveHandler e, Function(f) (f + 1) : AddHandler e, Function(f) f : RaiseEvent e()"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Reorder [RemoveHandler e, Function(f) f]@38 -> @8",
                "Update [Function(f) f]@55 -> [Function(f) (f + 1)]@25")
        End Sub

        <Fact>
        Public Sub ExpressionStatements()
            Dim src1 = "Call F(Function(a) a)"
            Dim src2 = "F(Function(a) (a + 1))"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [Call F(Function(a) a)]@8 -> [F(Function(a) (a + 1))]@8",
                "Update [Function(a) a]@15 -> [Function(a) (a + 1)]@10")
        End Sub

        <Fact>
        Public Sub ThrowReturn()
            Dim src1 = "Throw F(Function(a) a) : Return Function(b) b"
            Dim src2 = "Throw F(Function(b) b) : Return Function(a) a"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Move [Function(b) b]@40 -> @16",
                "Move [Function(a) a]@16 -> @40")
        End Sub

        <Fact>
        Public Sub OnErrorGoToLabel()
            Dim src1 = "On Error GoTo ErrorHandler : Exit Sub : On Error GoTo label1 : " & vbLf & "label1:" & vbLf & "Resume Next"
            Dim src2 = "On Error GoTo -1 : On Error GoTo 0 : Exit Sub : " & vbLf & "label2:" & vbLf & "Resume"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Reorder [On Error GoTo label1]@48 -> @8",
                "Update [On Error GoTo label1]@48 -> [On Error GoTo -1]@8",
                "Update [On Error GoTo ErrorHandler]@8 -> [On Error GoTo 0]@27",
                "Update [label1:]@72 -> [label2:]@57",
                "Update [Resume Next]@80 -> [Resume]@65")
        End Sub

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/75231")>
        Public Sub XmlLiteral_Text()
            Dim src1 = "
Dim a = <x>Text1</x>
"
            Dim src2 = "
Dim a = <x>Text2</x>
"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [a = <x>Text1</x>]@14 -> [a = <x>Text2</x>]@14")
        End Sub

        <Fact>
        Public Sub XmlLiteral_Node()
            Dim src1 = "
Dim a = <x>Text</x>
"
            Dim src2 = "
Dim a = <y>Text</y>
"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [a = <x>Text</x>]@14 -> [a = <y>Text</y>]@14")
        End Sub

        <Fact>
        Public Sub XmlLiteral_AttributeValue()
            Dim src1 = "
Dim a = <x a=""attr1"">Text</x>
"
            Dim src2 = "
Dim a = <x a=""attr2"">Text</x>
"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [a = <x a=""attr1"">Text</x>]@14 -> [a = <x a=""attr2"">Text</x>]@14")
        End Sub

        <Fact>
        Public Sub XmlLiteral_AttributeName()
            Dim src1 = "
Dim a = <x a=""attr"">Text</x>
"
            Dim src2 = "
Dim a = <x b=""attr"">Text</x>
"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [a = <x a=""attr"">Text</x>]@14 -> [a = <x b=""attr"">Text</x>]@14")
        End Sub

        <Fact>
        Public Sub XmlLiteral_CDATA()
            Dim src1 = "
Dim a = <x><![CDATA[Text1]]></x>
"
            Dim src2 = "
Dim a = <x><![CDATA[Text2]]></x>
"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [a = <x><![CDATA[Text1]]></x>]@14 -> [a = <x><![CDATA[Text2]]></x>]@14")
        End Sub

        <Fact>
        Public Sub XmlLiteral_Comment()
            Dim src1 = "
Dim a = <x><!--Text1--></x>
"
            Dim src2 = "
Dim a = <x><!--Text2--></x>
"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [a = <x><!--Text1--></x>]@14 -> [a = <x><!--Text2--></x>]@14")
        End Sub

#End Region

#Region "Select"

        <Fact>
        Public Sub Select_Reorder1()
            Dim src1 = "Select Case a : Case 1 : f() : End Select : " &
                       "Select Case b : Case 2 : g() : End Select"

            Dim src2 = "Select Case b : Case 2 : f() : End Select : " &
                       "Select Case a : Case 1 : g() : End Select"

            Dim edits = GetMethodEdits(src1, src2)
            edits.VerifyEdits(
                "Reorder [Select Case b : Case 2 : g() : End Select]@52 -> @8",
                "Move [f()]@33 -> @33",
                "Move [g()]@77 -> @77")
        End Sub

        <Fact>
        Public Sub Select_Case_Reorder()
            Dim src1 = "Select Case expr : Case 1 :      f() : Case 2, 3, 4 : g() : End Select"
            Dim src2 = "Select Case expr : Case 2, 3, 4: g() : Case 1       : f() : End Select"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Reorder [Case 2, 3, 4 : g()]@47 -> @27")
        End Sub

        <Fact>
        Public Sub Select_Case_Update()
            Dim src1 = "Select Case expr : Case 1 : f() : End Select"
            Dim src2 = "Select Case expr : Case 2 : f() : End Select"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [1]@32 -> [2]@32")
        End Sub

#End Region

#Region "Try, Catch, Finally"

        <Fact>
        Public Sub TryInsert1()
            Dim src1 = "x += 1"
            Dim src2 = "Try : x += 1 : Catch : End Try"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [Try : x += 1 : Catch : End Try]@8",
                "Insert [Try]@8",
                "Move [x += 1]@8 -> @14",
                "Insert [Catch]@23",
                "Insert [End Try]@31",
                "Insert [Catch]@23")
        End Sub

        <Fact>
        Public Sub TryDelete1()
            Dim src1 = "Try : x += 1 : Catch : End Try"
            Dim src2 = "x += 1"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Move [x += 1]@14 -> @8",
                "Delete [Try : x += 1 : Catch : End Try]@8",
                "Delete [Try]@8",
                "Delete [Catch]@23",
                "Delete [Catch]@23",
                "Delete [End Try]@31")
        End Sub

        <Fact>
        Public Sub TryReorder()
            Dim src1 = "Try : x += 1 : Catch :  End Try : Try : y += 1 : Catch :::  End Try"
            Dim src2 = "Try : y += 1 : Catch :: End Try : Try : x += 1 : Catch :::: End Try"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Reorder [Try : y += 1 : Catch :::  End Try]@42 -> @8")
        End Sub

        <Fact>
        Public Sub Finally_DeleteHeader()
            Dim src1 = "Try : Catch e AS E1 : Finally : End Try"
            Dim src2 = "Try : Catch e AS E1 : End Try"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Delete [Finally]@30",
                "Delete [Finally]@30")
        End Sub

        <Fact>
        Public Sub Finally_InsertHeader()
            Dim src1 = "Try : Catch e AS E1 : End Try"
            Dim src2 = "Try : Catch e AS E1 : Finally : End Try"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [Finally]@30",
                "Insert [Finally]@30")
        End Sub

        <Fact>
        Public Sub CatchUpdate()
            Dim src1 = "Try : Catch e As Exception : End Try"
            Dim src2 = "Try : Catch e As IOException : End Try"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [Catch e As Exception]@14 -> [Catch e As IOException]@14")
        End Sub

        <Fact>
        Public Sub WhenUpdate()
            Dim src1 = "Try : Catch e As Exception When e.Message = ""a"" : End Try"
            Dim src2 = "Try : Catch e As Exception When e.Message = ""b"" : End Try"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [When e.Message = ""a""]@35 -> [When e.Message = ""b""]@35")
        End Sub

        <Fact>
        Public Sub WhenCatchUpdate()
            Dim src1 = "Try : Catch e As Exception When e.Message = ""a"" : End Try"
            Dim src2 = "Try : Catch e As IOException When e.Message = ""a"" : End Try"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [Catch e As Exception When e.Message = ""a""]@14 -> [Catch e As IOException When e.Message = ""a""]@14")
        End Sub

        <Fact>
        Public Sub CatchInsert()
            Dim src1 = "Try : Catch e As Exception : End Try"
            Dim src2 = "Try : Catch e As IOException : Catch e As Exception : End Try"

            Dim edits = GetMethodEdits(src1, src2)
            edits.VerifyEdits(
                "Insert [Catch e As IOException]@14",
                "Insert [Catch e As IOException]@14")
        End Sub

        <Fact>
        Public Sub WhenInsert()
            Dim src1 = "Try : Catch e As Exception : End Try"
            Dim src2 = "Try : Catch e As Exception When e.Message = ""a"" : End Try"

            Dim edits = GetMethodEdits(src1, src2)
            edits.VerifyEdits(
                "Insert [When e.Message = ""a""]@35")
        End Sub

        <Fact>
        Public Sub WhenDelete()
            Dim src1 = "Try : Catch e As Exception When e.Message = ""a"" : End Try"
            Dim src2 = "Try : Catch e As Exception : End Try"

            Dim edits = GetMethodEdits(src1, src2)
            edits.VerifyEdits(
                "Delete [When e.Message = ""a""]@35")
        End Sub

        <Fact>
        Public Sub CatchBodyUpdate()
            Dim src1 = "Try : Catch e As E : x += 1 : End Try"
            Dim src2 = "Try : Catch e As E : y += 1 : End Try"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [x += 1]@29 -> [y += 1]@29")
        End Sub

        <Fact>
        Public Sub CatchDelete()
            Dim src1 = "Try : Catch e As IOException : Catch e As Exception : End Try"
            Dim src2 = "Try : Catch e As IOException : End Try"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Delete [Catch e As Exception]@39",
                "Delete [Catch e As Exception]@39")
        End Sub

        <Fact>
        Public Sub CatchReorder()
            Dim src1 = "Try : Catch e As IOException : Catch e As Exception : End Try"
            Dim src2 = "Try : Catch e As Exception : Catch e As IOException : End Try"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Reorder [Catch e As Exception]@39 -> @14")
        End Sub

        <Fact>
        Public Sub CatchInsertDelete()
            Dim src1 = "Try : x += 1 : Catch e As E : Catch e As Exception : End Try : " &
                       "Try : Console.WriteLine() : Finally : End Try"

            Dim src2 = "Try : x += 1 : Catch e As Exception : End Try : " &
                       "Try : Console.WriteLine() : Catch e As E : Finally : End Try"

            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [Catch e As E]@84",
                "Insert [Catch e As E]@84",
                "Delete [Catch e As E]@23",
                "Delete [Catch e As E]@23")
        End Sub

        <Fact>
        Public Sub Catch_DeleteHeader1()
            Dim src1 = "Try : Catch e As E1 : Catch e As E2 : End Try"
            Dim src2 = "Try : Catch e As E1 : End Try"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Delete [Catch e As E2]@30",
                "Delete [Catch e As E2]@30")
        End Sub
#End Region

#Region "With"
        <Fact>
        Public Sub WithBlock_Insert()
            Dim src1 = ""
            Dim src2 = "With a : F(.x) : End With"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [With a : F(.x) : End With]@8",
                "Insert [With a]@8",
                "Insert [F(.x)]@17",
                "Insert [End With]@25")
        End Sub

        <Fact>
        Public Sub WithBlock_Delete()
            Dim src1 = "With a : F(.x) : End With"
            Dim src2 = ""
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Delete [With a : F(.x) : End With]@8",
                "Delete [With a]@8",
                "Delete [F(.x)]@17",
                "Delete [End With]@25")
        End Sub

        <Fact>
        Public Sub WithBlock_Reorder()
            Dim src1 = "With a : F(.x) : End With  :  With a : F(.y) : End With"
            Dim src2 = "With a : F(.y) : End With  :  With a : F(.x) : End With"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Reorder [With a : F(.y) : End With]@38 -> @8")
        End Sub
#End Region

#Region "Using"
        <Fact>
        Public Sub Using1()
            Dim src1 As String = "Using a : Using b : Goo() : End Using : End Using"
            Dim src2 As String = "Using a : Using c : Using b : Goo() : End Using : End Using : End Using"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [Using c : Using b : Goo() : End Using : End Using]@18",
                "Insert [Using c]@18",
                "Move [Using b : Goo() : End Using]@18 -> @28",
                "Insert [End Using]@58")
        End Sub

        <Fact>
        Public Sub Using_DeleteHeader()
            Dim src1 As String = "Using a : Goo() : End Using"
            Dim src2 As String = "Goo()"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Move [Goo()]@18 -> @8",
                "Delete [Using a : Goo() : End Using]@8",
                "Delete [Using a]@8",
                "Delete [End Using]@26")
        End Sub

        <Fact>
        Public Sub Using_InsertHeader()
            Dim src1 As String = "Goo()"
            Dim src2 As String = "Using a : Goo() : End Using"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [Using a : Goo() : End Using]@8",
                "Insert [Using a]@8",
                "Move [Goo()]@8 -> @18",
                "Insert [End Using]@26")
        End Sub
#End Region

#Region "SyncLock"
        <Fact>
        Public Sub SyncLock1()
            Dim src1 As String = "SyncLock a : SyncLock b : Goo() : End SyncLock : End SyncLock"
            Dim src2 As String = "SyncLock a : SyncLock c : SyncLock b : Goo() : End SyncLock : End SyncLock : End SyncLock"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [SyncLock c : SyncLock b : Goo() : End SyncLock : End SyncLock]@21",
                "Insert [SyncLock c]@21",
                "Move [SyncLock b : Goo() : End SyncLock]@21 -> @34",
                "Insert [End SyncLock]@70")
        End Sub

        <Fact>
        Public Sub SyncLock_DeleteHeader()
            Dim src1 As String = "SyncLock a : Goo() : End SyncLock"
            Dim src2 As String = "Goo()"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Move [Goo()]@21 -> @8",
                "Delete [SyncLock a : Goo() : End SyncLock]@8",
                "Delete [SyncLock a]@8",
                "Delete [End SyncLock]@29")
        End Sub

        <Fact>
        Public Sub SyncLock_InsertHeader()
            Dim src1 As String = "Goo()"
            Dim src2 As String = "SyncLock a : Goo() : End SyncLock"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [SyncLock a : Goo() : End SyncLock]@8",
                "Insert [SyncLock a]@8",
                "Move [Goo()]@8 -> @21",
                "Insert [End SyncLock]@29")
        End Sub
#End Region

#Region "For Each"
        <Fact>
        Public Sub ForEach1()
            Dim src1 As String = "For Each a In e : For Each b In f : Goo() : Next : Next"
            Dim src2 As String = "For Each a In e : For Each c In g : For Each b In f : Goo() : Next : Next : Next"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [For Each c In g : For Each b In f : Goo() : Next : Next]@26",
                "Insert [For Each c In g]@26",
                "Move [For Each b In f : Goo() : Next]@26 -> @44",
                "Insert [Next]@77")

            Dim actual = ToMatchingPairs(edits.Match)
            Dim expected = New MatchingPairs From
            {
                {"Sub F()", "Sub F()"},
                {"For Each a In e : For Each b In f : Goo() : Next : Next",
                 "For Each a In e : For Each c In g : For Each b In f : Goo() : Next : Next : Next"},
                {"For Each a In e",
                 "For Each a In e"},
                {"For Each b In f : Goo() : Next",
                 "For Each b In f : Goo() : Next"},
                {"For Each b In f",
                 "For Each b In f"},
                {"Goo()", "Goo()"},
                {"Next", "Next"},
                {"Next", "Next"},
                {"End Sub", "End Sub"}
            }

            expected.AssertEqual(actual)
        End Sub

        <Fact>
        Public Sub ForEach_Swap1()
            Dim src1 As String = "For Each a In e : For Each b In f : Goo() : Next : Next"
            Dim src2 As String = "For Each b In f : For Each a In e : Goo() : Next : Next"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Move [For Each b In f : Goo() : Next]@26 -> @8",
                "Move [For Each a In e : For Each b In f : Goo() : Next : Next]@8 -> @26",
                "Move [Goo()]@44 -> @44")

            Dim actual = ToMatchingPairs(edits.Match)
            Dim expected = New MatchingPairs From
            {
                {"Sub F()", "Sub F()"},
                {"For Each a In e : For Each b In f : Goo() : Next : Next", "For Each a In e : Goo() : Next"},
                {"For Each a In e", "For Each a In e"},
                {"For Each b In f : Goo() : Next", "For Each b In f : For Each a In e : Goo() : Next : Next"},
                {"For Each b In f", "For Each b In f"},
                {"Goo()", "Goo()"},
                {"Next", "Next"},
                {"Next", "Next"},
                {"End Sub", "End Sub"}
            }

            expected.AssertEqual(actual)
        End Sub

        <Fact>
        Public Sub Foreach_DeleteHeader()
            Dim src1 As String = "For Each a In b : Goo() : Next"
            Dim src2 As String = "Goo()"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Move [Goo()]@26 -> @8",
                "Delete [For Each a In b : Goo() : Next]@8",
                "Delete [For Each a In b]@8",
                "Delete [Next]@34")
        End Sub

        <Fact>
        Public Sub Foreach_InsertHeader()
            Dim src1 As String = "Goo()"
            Dim src2 As String = "For Each a In b : Goo() : Next"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [For Each a In b : Goo() : Next]@8",
                "Insert [For Each a In b]@8",
                "Move [Goo()]@8 -> @26",
                "Insert [Next]@34")
        End Sub
#End Region

#Region "For"
        <Fact>
        Public Sub For1()
            Dim src1 = "For a = 0 To 10 : For a = 0 To 20 : Goo() : Next : Next"
            Dim src2 = "For a = 0 To 10 : For b = 0 To 10 : For a = 0 To 20 : Goo() : Next : Next : Next"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [For b = 0 To 10 : For a = 0 To 20 : Goo() : Next : Next]@26",
                "Insert [For b = 0 To 10]@26",
                "Move [For a = 0 To 20 : Goo() : Next]@26 -> @44",
                "Insert [Next]@77")
        End Sub

        <Fact>
        Public Sub For2()
            Dim src1 = "For a = 0 To 10 Step 1 : For a = 0 To 20 : Goo() : Next : Next"
            Dim src2 = "For a = 0 To 10 Step 2 : For b = 0 To 10 Step 4 : For a = 0 To 20 Step 5 : Goo() : Next : Next : Next"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [For b = 0 To 10 Step 4 : For a = 0 To 20 Step 5 : Goo() : Next : Next]@33",
                "Update [Step 1]@24 -> [Step 2]@24",
                "Insert [For b = 0 To 10 Step 4]@33",
                "Move [For a = 0 To 20 : Goo() : Next]@33 -> @58",
                "Insert [Next]@98",
                "Insert [Step 4]@49",
                "Insert [Step 5]@74")
        End Sub

        <Fact>
        Public Sub For_DeleteHeader()
            Dim src1 As String = "For a = 0 To 10 : Goo() : Next"
            Dim src2 As String = "Goo()"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Move [Goo()]@26 -> @8",
                "Delete [For a = 0 To 10 : Goo() : Next]@8",
                "Delete [For a = 0 To 10]@8",
                "Delete [Next]@34")
        End Sub

        <Fact>
        Public Sub For_DeleteStep()
            Dim src1 As String = "For a = 0 To 10 Step 1 : Goo() : Next"
            Dim src2 As String = "For a = 0 To 10 : Goo() : Next"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Delete [Step 1]@24")
        End Sub

        <Fact>
        Public Sub For_InsertStep()
            Dim src1 As String = "For a = 0 To 10 : Goo() : Next"
            Dim src2 As String = "For a = 0 To 10 Step 1 : Goo() : Next"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [Step 1]@24")
        End Sub

        <Fact>
        Public Sub For_InsertHeader()
            Dim src1 As String = "Goo()"
            Dim src2 As String = "For a = 0 To 10 : Goo() : Next"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [For a = 0 To 10 : Goo() : Next]@8",
                "Insert [For a = 0 To 10]@8",
                "Move [Goo()]@8 -> @26",
                "Insert [Next]@34")
        End Sub
#End Region

#Region "Do, While, Loop"
        <Fact>
        Public Sub While1()
            Dim src1 As String = "While a : While b : Goo() : End While : End While"
            Dim src2 As String = "While a : While c : While b : Goo() : End While : End While : End While"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [While c : While b : Goo() : End While : End While]@18",
                "Insert [While c]@18",
                "Move [While b : Goo() : End While]@18 -> @28",
                "Insert [End While]@58")
        End Sub

        <Fact>
        Public Sub DoWhile1()
            Dim src1 As String = "While a : While b : Goo() : End While : End While"
            Dim src2 As String = "Do While a : While c : Do Until b : Goo() : Loop : End While : Loop"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [While a : While b : Goo() : End While : End While]@8 -> [Do While a : While c : Do Until b : Goo() : Loop : End While : Loop]@8",
                "Update [While a]@8 -> [Do While a]@8",
                "Insert [While c : Do Until b : Goo() : Loop : End While]@21",
                "Update [End While]@48 -> [Loop]@71",
                "Insert [While a]@11",
                "Insert [While c]@21",
                "Update [While b : Goo() : End While]@18 -> [Do Until b : Goo() : Loop]@31",
                "Move [While b : Goo() : End While]@18 -> @31",
                "Insert [End While]@59",
                "Update [While b]@18 -> [Do Until b]@31",
                "Update [End While]@36 -> [Loop]@52",
                "Insert [Until b]@34")
        End Sub

        <Fact>
        Public Sub While_DeleteHeader()
            Dim src1 As String = "While a : Goo() : End While"
            Dim src2 As String = "Goo()"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Move [Goo()]@18 -> @8",
                "Delete [While a : Goo() : End While]@8",
                "Delete [While a]@8",
                "Delete [End While]@26")
        End Sub

        <Fact>
        Public Sub While_InsertHeader()
            Dim src1 As String = "Goo()"
            Dim src2 As String = "While a : Goo() : End While"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [While a : Goo() : End While]@8",
                "Insert [While a]@8",
                "Move [Goo()]@8 -> @18",
                "Insert [End While]@26")
        End Sub

        <Fact>
        Public Sub Do1()
            Dim src1 = "Do : Do : Goo() : Loop While b : Loop Until a"
            Dim src2 = "Do : Do : Do : Goo() : Loop While b : Loop: Loop Until a"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [Do : Do : Goo() : Loop While b : Loop]@13",
                "Insert [Do]@13",
                "Move [Do : Goo() : Loop While b]@13 -> @18",
                "Insert [Loop]@46")
        End Sub

        <Fact>
        Public Sub Do_DeleteHeader()
            Dim src1 = "Do : Goo() : Loop"
            Dim src2 = "Goo()"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Move [Goo()]@13 -> @8",
                "Delete [Do : Goo() : Loop]@8",
                "Delete [Do]@8",
                "Delete [Loop]@21")
        End Sub

        <Fact>
        Public Sub Do_InsertHeader()
            Dim src1 = "Goo()"
            Dim src2 = "Do : Goo() : Loop"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [Do : Goo() : Loop]@8",
                "Insert [Do]@8",
                "Move [Goo()]@8 -> @13",
                "Insert [Loop]@21")
        End Sub
#End Region

#Region "If"
        <Fact>
        Public Sub IfStatement_TestExpression_Update1()
            Dim src1 = "Dim x = 1 : If x = 1 : x += 1 : End If"
            Dim src2 = "Dim x = 1 : If x = 2 : x += 1 : End If"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [If x = 1]@20 -> [If x = 2]@20")
        End Sub

        <Fact>
        Public Sub IfStatement_TestExpression_Update2()
            Dim src1 = "Dim x = 1 : If x = 1 Then x += 1" & vbLf
            Dim src2 = "Dim x = 1 : If x = 2 Then x += 1" & vbLf
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [If x = 1 Then x += 1]@20 -> [If x = 2 Then x += 1]@20")
        End Sub

        <Fact>
        Public Sub IfStatement_TestExpression_Update3()
            Dim src1 = "Dim x = 1 : If x = 1 : x += 1 : End If" & vbLf
            Dim src2 = "Dim x = 1 : If x = 2 Then x += 1" & vbLf
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [If x = 1 : x += 1 : End If]@20 -> [If x = 2 Then x += 1]@20",
                "Delete [If x = 1]@20",
                "Delete [End If]@40")
        End Sub

        <Fact>
        Public Sub ElseClause_Insert()
            Dim src1 = "If x = 1 : x += 1 : End If"
            Dim src2 = "If x = 1 : x += 1 : Else : y += 1 : End If"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [Else : y += 1]@28",
                "Insert [Else]@28",
                "Insert [y += 1]@35")
        End Sub

        <Fact>
        Public Sub ElseClause_InsertMove()
            Dim src1 = "If x = 1 : x += 1 : Else : y += 1 : End If"
            Dim src2 = "If x = 1 : x += 1 : ElseIf x = 2 : y += 1 : End If"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [ElseIf x = 2 : y += 1]@28",
                "Insert [ElseIf x = 2]@28",
                "Move [y += 1]@35 -> @43",
                "Delete [Else : y += 1]@28",
                "Delete [Else]@28")
        End Sub

        <Fact>
        Public Sub If1()
            Dim src1 As String = "If a : If b : Goo() : End If : End If"
            Dim src2 As String = "If a : If c : If b : Goo() : End If : End If : End If"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [If c : If b : Goo() : End If : End If]@15",
                "Insert [If c]@15",
                "Move [If b : Goo() : End If]@15 -> @22",
                "Insert [End If]@46")
        End Sub

        <Fact>
        Public Sub If_DeleteHeader()
            Dim src1 = "If a : Goo() : End If"
            Dim src2 = "Goo()"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Move [Goo()]@15 -> @8",
                "Delete [If a : Goo() : End If]@8",
                "Delete [If a]@8",
                "Delete [End If]@23")
        End Sub

        <Fact>
        Public Sub If_InsertHeader()
            Dim src1 = "Goo()"
            Dim src2 = "If a : Goo() : End If"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [If a : Goo() : End If]@8",
                "Insert [If a]@8",
                "Move [Goo()]@8 -> @15",
                "Insert [End If]@23")
        End Sub

        <Fact>
        Public Sub Else_DeleteHeader()
            Dim src1 As String = "If a : Goo( ) : Else : Goo(  ) : End If"
            Dim src2 As String = "If a : Goo( ) : Goo(  ) : End If"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Move [Goo(  )]@31 -> @24",
                "Delete [Else : Goo(  )]@24",
                "Delete [Else]@24")
        End Sub

        <Fact>
        Public Sub Else_InsertHeader()
            Dim src1 = "If a : Goo( ) : End If : Goo(  )"
            Dim src2 = "If a : Goo( ) : Else : Goo(  ) : End If"

            Dim edits = GetMethodEdits(src1, src2)
            edits.VerifyEdits(
                "Insert [Else : Goo(  )]@24",
                "Insert [Else]@24",
                "Move [Goo(  )]@33 -> @31")
        End Sub

        <Fact>
        Public Sub ElseIf_DeleteHeader()
            Dim src1 = "If a : Goo( ) : ElseIf b : Goo(  ) : End If"
            Dim src2 = "If a : Goo( ) : End If : Goo(  )"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Move [Goo(  )]@35 -> @33",
                "Delete [ElseIf b : Goo(  )]@24",
                "Delete [ElseIf b]@24")
        End Sub

        <Fact>
        Public Sub ElseIf_InsertHeader()
            Dim src1 = "If a : Goo( ) : Goo(  ) : End If"
            Dim src2 = "If a : Goo( ) : Else If b : Goo(  ) : End If"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Insert [Else If b : Goo(  )]@24",
                "Insert [Else If b]@24",
                "Move [Goo(  )]@24 -> @36")
        End Sub
#End Region

#Region "Lambdas"
        <Fact>
        Public Sub Lambdas_InVariableDeclarator()
            Dim src1 = "Dim x = Function(a) a, y = Function(b) b"
            Dim src2 = "Dim x = Sub(a) a, y = Function(b) (b + 1)"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [Function(a) a]@16 -> [Sub(a) a]@16",
                "Update [Function(b) b]@35 -> [Function(b) (b + 1)]@30")
        End Sub

        <Fact>
        Public Sub Lambdas_InExpressionStatement()
            Dim src1 = "F(Function(a) a, Function(b) b)"
            Dim src2 = "F(Function(b) b, Function(a)(a+1))"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Reorder [Function(b) b]@25 -> @10",
                "Update [Function(a) a]@10 -> [Function(a)(a+1)]@25")
        End Sub

        <Fact>
        Public Sub Lambdas_InWhile()
            Dim src1 = "While F(Function(a) a) : End While"
            Dim src2 = "Do : Loop While F(Function(a) a)"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [While F(Function(a) a) : End While]@8 -> [Do : Loop While F(Function(a) a)]@8",
                "Update [While F(Function(a) a)]@8 -> [Do]@8",
                "Update [End While]@33 -> [Loop While F(Function(a) a)]@13",
                "Insert [While F(Function(a) a)]@18",
                "Move [Function(a) a]@16 -> @26")
        End Sub

        <Fact>
        Public Sub Lambdas_InLambda()
            Dim src1 = "F(Sub()" & vbLf & "G(Function(x) y) : End Sub)"
            Dim src2 = "F(Function(q) G(Sub(x) f()))"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [Sub()" & vbLf & "G(Function(x) y) : End Sub]@10 -> [Function(q) G(Sub(x) f())]@10")
        End Sub

        <Fact>
        Public Sub Lambdas_Insert_Static_Top()
            Dim src1 = "
Imports System

Class C
    Sub F()
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C
    Sub F()
        Dim f = new Func(Of Integer, Integer)(Function(a) a)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)

            edits.VerifySemanticDiagnostics(
                capabilities:=
                    EditAndContinueCapabilities.AddMethodToExistingType Or
                    EditAndContinueCapabilities.AddStaticFieldToExistingType Or
                    EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Insert_Static_Nested1()
            Dim src1 = "
Imports System

Class C
    Shared Function G(f As Func(Of Integer, Integer)) As Integer
        Return 0
    End Function

    Sub F()
        G(Function(a) a)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C
    Shared Function G(f As Func(Of Integer, Integer)) As Integer
        Return 0
    End Function

    Sub F()
        G(Function(a) G(Function(b) b) + a)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics(
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.AddStaticFieldToExistingType)
        End Sub

        <Fact>
        Public Sub Lambdas_Insert_ThisOnly_Top1()
            Dim src1 = "
Imports System

Class C
    Dim x As Integer = 0

    Function G(f As Func(Of Integer, Integer)) As Integer
        Return 0
    End Function

    Sub F()
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C
    Dim x As Integer = 0

    Function G(f As Func(Of Integer, Integer)) As Integer
        Return 0
    End Function

    Sub F()
        G(Function(a) x)
    End Sub
End Class"
            Dim edits = GetTopEdits(src1, src2)

            edits.VerifySemantics(
                {SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)},
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Insert_ThisOnly_Top2()
            Dim src1 = "
Imports System
Imports System.Linq

Class C
    Sub F()
        Dim y As Integer = 1
        Do
            Dim x As Integer = 2
            Dim f1 = New Func(Of Integer, Integer)(Function(a) y)
        Loop
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq

Class C
    Sub F()
        Dim y As Integer = 1
        Do
            Dim x As Integer = 2
            Dim f2 = From a In {1} Select a + y
            Dim f3 = From a In {1} Where x > 0 Select a
        Loop
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                {SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)},
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Insert_ThisOnly_Nested1()
            Dim src1 = "
Imports System

Class C
    Dim x As Integer = 0

    Function G(f As Func(Of Integer, Integer)) As Integer
        Return 0
    End Function

    Sub F()
        G(Function(a) a)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C
    Dim x As Integer = 0

    Function G(f As Func(Of Integer, Integer)) As Integer
        Return 0
    End Function

    Sub F()
        G(Function(a) G(Function(b) x))
    End Sub
End Class

"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                {SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)},
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Insert_ThisOnly_Nested2()
            Dim src1 = "
Imports System

Class C
    Dim x As Integer = 0

    Sub F()
        Dim f1 = New Func(Of Integer, Integer)(
            Function(a)
                Dim f2 = New Func(Of Integer, Integer)(
                    Function(b)
                        Return b
                    End Function)
                Return a
            End Function)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C

    Dim x As Integer = 0

    Sub F()
        Dim f1 = New Func(Of Integer, Integer)(
            Function(a)
                Dim f2 = New Func(Of Integer, Integer)(
                    Function(b)
                        Return b
                    End Function)

                Dim f3 = New Func(Of Integer, Integer)(
                    Function(c)
                        Return c + x
                    End Function)
                Return a
            End Function)
    End Sub
End Class
"

            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                {SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)},
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_InsertAndDelete_Scopes1()
            Dim src1 = "
Imports System

Class C
    Sub G(f As Func(Of Integer, Integer))
    End Sub

    Dim x As Integer = 0, y As Integer = 0                   ' Group #0

    Sub F()
        Dim x0 As Integer = 0, y0 As Integer = 0             ' Group #1
        Do
            Dim x1 As Integer = 0, y1 As Integer = 0         ' Group #2
            Do
                Dim x2 As Integer = 0, y2 As Integer = 0     ' Group #1
                Do
                    Dim x3 As Integer = 0, y3 As Integer = 0 ' Group #2

                    G(Function(a) x3 + x1)
                    G(Function(a) x0 + y0 + x2)
                    G(Function(a) x)
                Loop
            Loop
        Loop
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C
    Sub G(f As Func(Of Integer, Integer))
    End Sub

    Dim x As Integer = 0, y As Integer = 0                   ' Group #0

    Sub F()
        Dim x0 As Integer = 0, y0 As Integer = 0             ' Group #1
        Do
            Dim x1 As Integer = 0, y1 As Integer = 0         ' Group #2
            Do
                Dim x2 As Integer = 0, y2 As Integer = 0     ' Group #1
                Do
                    Dim x3 As Integer = 0, y3 As Integer = 0 ' Group #2

                    G(Function(a) x3 + x1)
                    G(Function(a) x0 + y0 + x2)
                    G(Function(a) x)

                    G(Function(a) x)                         ' OK
                    G(Function(a) x0 + y0)                   ' OK
                    G(Function(a) x1 + y0)                   ' runtime rude edit - connecting Group #1 and Group #2
                    G(Function(a) x3 + x1)                   ' runtime rude edit - multi-scope (conservative)
                    G(Function(a) x + y0)                    ' runtime rude edit - connecting Group #0 and Group #1
                    G(Function(a) x + x3)                    ' runtime rude edit - connecting Group #0 and Group #2
                Loop
            Loop
        Loop
    End Sub
End Class"

            Dim insert = GetTopEdits(src1, src2)
            insert.VerifySemantics(
                {SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)},
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)

            Dim delete = GetTopEdits(src2, src1)
            delete.VerifySemantics(
                {SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)},
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/1290")>
        Public Sub Lambdas_Update_Signature1()
            Dim src1 = "
Imports System

Class C
    Sub G1(f As Func(Of Integer, Integer))
    End Sub

    Sub G2(f As Func(Of Long, Long))
    End Sub

    Sub F()
        G1(<N:0>Function(a) a</N:0>)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C
    Sub G1(f As Func(Of Integer, Integer))
    End Sub

    Sub G2(f As Func(Of Long, Long))
    End Sub

    Sub F()
        G2(<N:0>Function(a) a</N:0>)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.NodePosition(0), {GetResource("lambda")})
                }))
        End Sub

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/1290")>
        Public Sub Lambdas_Update_Signature2()
            Dim src1 = "
Imports System

Class C
    Sub G1(f As Func(Of Integer, Integer))
    End Sub

    Sub G2(f As Func(Of Integer, Integer, Integer))
    End Sub

    Sub F()
        G1(<N:0>Function(a) a</N:0>)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C
    Sub G1(f As Func(Of Integer, Integer))
    End Sub

    Sub G2(f As Func(Of Integer, Integer, Integer))
    End Sub

    Sub F()
        G2(<N:0>Function(a, b) a + b</N:0>)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.NodePosition(0), {GetResource("lambda")})
                }))
        End Sub

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/1290")>
        Public Sub Lambdas_Update_Signature3()
            Dim src1 = "
Imports System

Class C
    Sub G1(f As Func(Of Integer, Integer))
    End Sub

    Sub G2(f As Func(Of Integer, Long))
    End Sub

    Sub F()
        G1(<N:0>Function(a) a</N:0>)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C
    Sub G1(f As Func(Of Integer, Integer))
    End Sub

    Sub G2(f As Func(Of Integer, Long))
    End Sub

    Sub F()
        G2(<N:0>Function(a) a</N:0>)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
            {
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaReturnType, syntaxMap.NodePosition(0), {GetResource("lambda")})
                })
            })
        End Sub

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/1290")>
        Public Sub Lambdas_Update_Signature_EmptyBody1()
            Dim src1 = "
Imports System

Class C
    Sub G1(f As Action(Of Integer))
    End Sub

    Sub G2(f As Action(Of Long))
    End Sub

    Sub F()
        G1(
<N:0>Sub(a)
End Sub</N:0>)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C
    Sub G1(f As Action(Of Integer))
    End Sub

    Sub G2(f As Action(Of Long))
    End Sub

    Sub F()
        G2(
<N:0>Sub(a)
End Sub</N:0>)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.NodePosition(0), {GetResource("lambda")})
                }))
        End Sub

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/1290")>
        Public Sub Lambdas_Update_Signature_EmptyBody2()
            Dim src1 = "
Imports System

Class C
    Sub G1(f As Action)
    End Sub

    Sub G2(f As Func(Of Object))
    End Sub

    Sub F()
        G1(
<N:0>Sub()
End Sub</N:0>)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C
    Sub G1(f As Action)
    End Sub

    Sub G2(f As Func(Of Object))
    End Sub

    Sub F()
        G2(
<N:0>Function()
End Function</N:0>)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
            {
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaReturnType, syntaxMap.NodePosition(0), {GetResource("lambda")})
                })
            })
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Signature_SyntaxOnly1()
            Dim src1 = "
Imports System

Class C
    Sub G1(f As Func(Of Integer, Integer))
    End Sub

    Sub G2(f As Func(Of Integer, Integer))
    End Sub

    Sub F()
        G1(Function(a) a)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C
    Sub G1(f As Func(Of Integer, Integer))
    End Sub

    Sub G2(f As Func(Of Integer, Integer))
    End Sub

    Sub F()
        G2(Function(a) a)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/1290")>
        Public Sub Lambdas_Update_Signature_ReturnType1()
            Dim src1 = "
Imports System

Class C
    Sub G1(f As Func(Of Integer, Integer))
    End Sub

    Sub G2(f As Action(Of Integer))
    End Sub

    Sub F()
        G1(<N:0>Function(a)
            Return 1
        End Function</N:0>)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C
    Sub G1(f As Func(Of Integer, Integer))
    End Sub

    Sub G2(f As Action(Of Integer))
    End Sub

    Sub F()
        G2(<N:0>Sub(a)
           End Sub</N:0>)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
            {
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaReturnType, syntaxMap.NodePosition(0), {GetResource("lambda")})
                })
            })
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Signature_BodySyntaxOnly()
            Dim src1 = "
Imports System

Class C
    Sub G1(f As Func(Of Integer, Integer))
    End Sub

    Sub G2(f As Func(Of Integer, Integer))
    End Sub

    Sub F()
        G1(Function(a)
               Return 1
           End Function)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C
    Sub G1(f As Func(Of Integer, Integer))
    End Sub

    Sub G2(f As Func(Of Integer, Integer))
    End Sub

    Sub F()
        G2(Function(a) 2)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Signature_ParameterName1()
            Dim src1 = "
Imports System

Class C
    Sub G1(f As Func(Of Integer, Integer))
    End Sub

    Sub G2(f As Func(Of Integer, Integer))
    End Sub

    Sub F()
        G1(Function(a) 1)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C
    Sub G1(f As Func(Of Integer, Integer))
    End Sub

    Sub G2(f As Func(Of Integer, Integer))
    End Sub

    Sub F()
        G2(Function(b) 2)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/1290")>
        Public Sub Lambdas_Update_Signature_ParameterRefness1()
            Dim src1 = "
Imports System

Delegate Function D1(ByRef a As Integer) As Integer
Delegate Function D2(a As Integer) As Integer

Class C
    Sub G1(f As D1)
    End Sub

    Sub G2(f As D2)
    End Sub

    Sub F()
        G1(<N:0>Function(ByRef a As Integer) 1</N:0>)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Delegate Function D1(ByRef a As Integer) As Integer
Delegate Function D2(a As Integer) As Integer

Class C
    Sub G1(f As D1)
    End Sub

    Sub G2(f As D2)
    End Sub

    Sub F()
        G2(<N:0>Function(a As Integer) 2</N:0>)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.NodePosition(0), {GetResource("lambda")})
                }))
        End Sub

        <Fact>
        Public Sub Lambdas_Update_DelegateType1()
            Dim src1 = "
Imports System

Delegate Function D1(a As Integer) As Integer
Delegate Function D2(a As Integer) As Integer

Class C
    Sub G1(f As D1)
    End Sub

    Sub G2(f As D2)
    End Sub

    Sub F()
        G1(Function(a) a)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Delegate Function D1(a As Integer) As Integer
Delegate Function D2(a As Integer) As Integer

Class C

    Sub G1(f As D1)
    End Sub

    Sub G2(f As D2)
    End Sub

    Sub F()
        G2(Function(a) a)
    End Sub
End Class
"

            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact>
        Public Sub Lambdas_Update_SourceType1()
            Dim src1 = "
Imports System

Delegate Function D1(a As C) As C
Delegate Function D2(a As C) As C

Class C
    Sub G1(f As D1)
    End Sub

    Sub G2(f As D2)
    End Sub

    Sub F()
        G1(Function(a) a)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Delegate Function D1(a As C) As C
Delegate Function D2(a As C) As C

Class C
    Sub G1(f As D1)
    End Sub

    Sub G2(f As D2)
    End Sub

    Sub F()
        G2(Function(a) a)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/1290")>
        Public Sub Lambdas_Update_SourceType2()
            Dim src1 = "
Imports System

Delegate Function D1(a As C) As C
Delegate Function D2(a As B) As B

Class B
End Class

Class C
    Sub G1(f As D1)
    End Sub

    Sub G2(f As D2)
    End Sub

    Sub F()
        G1(<N:0>Function(a) a</N:0>)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Delegate Function D1(a As C) As C
Delegate Function D2(a As B) As B

Class B
End Class

Class C
    Sub G1(f As D1)
    End Sub

    Sub G2(f As D2)
    End Sub

    Sub F()
        G2(<N:0>Function(a) a</N:0>)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.NodePosition(0), {GetResource("lambda")})
                }))
        End Sub

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/1290")>
        Public Sub Lambdas_Update_SourceTypeAndMetadataType1()
            Dim src1 = "
Namespace [System]

    Delegate Function D1(a As String) As String
    Delegate Function D2(a As [String]) As [String]

    Class [String]
    End Class

    Class C

        Sub G1(f As D1)
        End Sub

        Sub G2(f As D2)
        End Sub

        Sub F()
            G1(<N:0>Function(a) a</N:0>)
        End Sub
    End Class
End Namespace
"
            Dim src2 = "
Namespace [System]

    Delegate Function D1(a As String) As String
    Delegate Function D2(a As [String]) As [String]

    Class [String]
    End Class

    Class C
        Sub G1(f As D1)
        End Sub

        Sub G2(f As D2)
        End Sub

        Sub F()
            G2(<N:0>Function(a) a</N:0>)
        End Sub
    End Class
End Namespace
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("System.C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.NodePosition(0), {GetResource("lambda")})
                }))
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Generic1()
            Dim src1 = "
Delegate Function D1(Of S, T)(a As S, b As T) As T
Delegate Function D2(Of S, T)(a As T, b As S) As T

Class C

    Sub G1(f As D1(Of Integer, Integer))
    End Sub

    Sub G2(f As D2(Of Integer, Integer))
    End Sub

    Sub F()
        G1(Function(a, b) a + b)
    End Sub
End Class
"
            Dim src2 = "
Delegate Function D1(Of S, T)(a As S, b As T) As T
Delegate Function D2(Of S, T)(a As T, b As S) As T

Class C
    Sub G1(f As D1(Of Integer, Integer))
    End Sub

    Sub G2(f As D2(Of Integer, Integer))
    End Sub

    Sub F()
        G2(Function(a, b) a + b)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/1290")>
        Public Sub Lambdas_Update_Generic2()
            Dim src1 = "
Delegate Function D1(Of S, T)(a As S, b As T) As Integer
Delegate Function D2(Of S, T)(a As T, b As S) As Integer

Class C
    Sub G1(f As D1(Of Integer, Integer))
    End Sub

    Sub G2(f As D2(Of Integer, String))
    End Sub

    Sub F()
        G1(<N:0>Function(a, b) 1</N:0>)
    End Sub
End Class
"
            Dim src2 = "
Delegate Function D1(Of S, T)(a As S, b As T) As Integer
Delegate Function D2(Of S, T)(a As T, b As S) As Integer

Class C
    Sub G1(f As D1(Of Integer, Integer))
    End Sub

    Sub G2(f As D2(Of Integer, String))
    End Sub

    Sub F()
        G2(<N:0>Function(a, b) 1</N:0>)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.NodePosition(0), {GetResource("lambda")})
                }))
        End Sub

        <Fact>
        Public Sub Lambdas_Update_CapturedParameters1()
            Dim src1 = "
Imports System
Class C
    Sub F(x1 As Integer)
        Dim f1 = New Func(Of Integer, Integer, Integer)(
            Function(a1, a2)
                Dim f2 = New Func(Of Integer, Integer)(Function(a3) x1 + a2)
                Return a1
            End Function)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub F(x1 As Integer)
        Dim f1 = New Func(Of Integer, Integer, Integer)(
            Function(a1, a2)
                Dim f2 = New Func(Of Integer, Integer)(Function(a3) x1 + a2 + 1)
                Return a1
            End Function)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/2223")>
        Public Sub Lambdas_Update_CapturedParameters2()
            Dim src1 = "
Imports System
Class C
    Sub F(x1 As Integer)
        Dim f1 = New Func(Of Integer, Integer, Integer)(
            Function(a1, a2)
                Dim f2 = New Func(Of Integer, Integer)(Function(a3) x1 + a2)
                Return a1
            End Function)

        Dim f3 = New Func(Of Integer, Integer, Integer)(
            Function(a1, a2)
                Dim f4 = New Func(Of Integer, Integer)(Function(a3) x1 + a2)
                Return a1
            End Function)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub F(x1 As Integer)
        Dim f1 = New Func(Of Integer, Integer, Integer)(
            Function(a1, a2)
                Dim f2 = New Func(Of Integer, Integer)(Function(a3) x1 + a2 + 1)
                Return a1
            End Function)

        Dim f3 = New Func(Of Integer, Integer, Integer)(
            Function(a1, a2)
                Dim f4 = New Func(Of Integer, Integer)(Function(a3) x1 + a2 + 1)
                Return a1
            End Function)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact>
        Public Sub Lambdas_Update_CeaseCapture_This()
            Dim src1 = "
Imports System

Class C
    Dim x As Integer = 1

    Sub F()
        Dim f = New Func(Of Integer, Integer)(Function(a) a + x)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C
    Dim x As Integer = 1

    Sub F()
        Dim f = New Func(Of Integer, Integer)(Function(a) a)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_CeaseCapture_Closure1()
            Dim src1 = "
Imports System
Class C
    Sub F()
        Dim y As Integer = 1
        Dim f1 = New Func(Of Integer, Integer)(
            Function(a1)
                Dim f2 = New Func(Of Integer, Integer)(Function(a2) y + a2)
                Return a1
            End Function)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub F()
        Dim y As Integer = 1
        Dim f1 = New Func(Of Integer, Integer)(
            Function(a1)
                Dim f2 = New Func(Of Integer, Integer)(Function(a2) a2)
                Return a1 + y
            End Function)
    End Sub
End Class

"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_CeaseCapture_IndexerParameter2()
            Dim src1 = "
Imports System
Class C
    Readonly Property Item(a1 As Integer, a2 As Integer) As Func(Of Integer, Integer)
        Get
            Return New Func(Of Integer, Integer)(Function(a3) a1 + a2)
        End Get
    End Property
End Class
"
            Dim src2 = "
Imports System
Class C
    Readonly Property Item(a1 As Integer, a2 As Integer) As Func(Of Integer, Integer)
        Get
            Return New Func(Of Integer, Integer)(Function(a3) a2)
        End Get
    End Property
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.get_Item"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_CeaseCapture_IndexerParameter_Delete()
            Dim src1 = "
Imports System
Class C
    Readonly Property Item(a1 As Integer, a2 As Integer) As Func(Of Integer, Integer)
        Get
            Return New Func(Of Integer, Integer)(Function(a3) a1 + a2)
        End Get
    End Property
End Class
"
            Dim src2 = "
Imports System
Class C
    Readonly Property Item(a2 As Integer) As Func(Of Integer, Integer)
        Get
            Return New Func(Of Integer, Integer)(Function(a3) a2)
        End Get
    End Property
End Class
"
            Dim edits = GetTopEdits(src1, src2)

            edits.VerifySemantics(
                {
                    SemanticEdit(SemanticEditKind.Delete, Function(c) c.GetMember("C.get_Item"), deletedSymbolContainerProvider:=Function(c) c.GetMember("C")),
                    SemanticEdit(SemanticEditKind.Insert, Function(c) c.GetMember("C.get_Item")),
                    SemanticEdit(SemanticEditKind.Delete, Function(c) c.GetMember("C.Item"), deletedSymbolContainerProvider:=Function(c) c.GetMember("C")),
                    SemanticEdit(SemanticEditKind.Insert, Function(c) c.GetMember("C.Item"))
                },
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Update_CeaseCapture_IndexerParameter_Setter_WithExplicitValue()
            Dim src1 = "
Class C
    WriteOnly Property Item(a1 As Integer, a2 As Integer) As Integer
        Set(Value As Integer)
            Dim f = Function() a1 + a2 + Value
        End Set
    End Property
End Class
"
            Dim src2 = "
Class C
    WriteOnly Property Item(a1 As Integer, a2 As Integer) As Integer
        Set(Value As Integer)
            Dim f = Function() a1
        End Set
    End Property
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.set_Item"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_CeaseCapture_IndexerParameter_Setter_WithImplicitValue()
            Dim src1 = "
Class C
    WriteOnly Property Item(a1 As Integer, a2 As Integer) As Integer
        Set
            Dim f = Function() a1 + a2 + Value
        End Set
    End Property
End Class
"
            Dim src2 = "
Class C
    WriteOnly Property Item(a1 As Integer, a2 As Integer) As Integer
        Set
            Dim f = Function() a1
        End Set
    End Property
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.set_Item"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_CeaseCapture_IndexerParameter_Setter_WithImplicitToExplicitValue()
            Dim src1 = "
Class C
    WriteOnly Property Item(a1 As Integer, a2 As Integer) As Integer
        Set
            Dim f = Function() a1 + a2 + Value
        End Set
    End Property
End Class
"
            Dim src2 = "
Class C
    WriteOnly Property Item(a1 As Integer, a2 As Integer) As Integer
        Set(Value As Integer)
            Dim f = Function() a1
        End Set
    End Property
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.set_Item"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_CeaseCapture_IndexerParameter_Setter_WithExplicitToImplicitValue()
            Dim src1 = "
Class C
    WriteOnly Property Item(a1 As Integer, a2 As Integer) As Integer
        Set(Value As Integer)
            Dim f = Function() a1 + a2 + Value
        End Set
    End Property
End Class
"
            Dim src2 = "
Class C
    WriteOnly Property Item(a1 As Integer, a2 As Integer) As Integer
        Set
            Dim f = Function() a1
        End Set
    End Property
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.set_Item"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_CeaseCapture_MethodParameter()
            Dim src1 = "
Imports System
Class C
    Sub F(a1 As Integer, a2 As Integer)
        Dim f2 = New Func(Of Integer, Integer)(Function(a3) a1 + a2)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub F(a1 As Integer, a2 As Integer)
        Dim f2 = New Func(Of Integer, Integer)(Function(a3) a1)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_CeaseCapture_MethodParameter_ParameterDelete()
            Dim src1 = "
Imports System
Class C
    Sub F(a1 As Integer, a2 As Integer)
        Dim f2 = New Func(Of Integer, Integer)(Function(a3) a1 + a2)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub F(a1 As Integer)
        Dim f2 = New Func(Of Integer, Integer)(Function(a3) a1)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                {
                    SemanticEdit(SemanticEditKind.Delete, Function(c) c.GetMember("C.F"), deletedSymbolContainerProvider:=Function(c) c.GetMember("C")),
                    SemanticEdit(SemanticEditKind.Insert, Function(c) c.GetMember("C.F"))
                },
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Update_CeaseCapture_MethodParameter_ParameterTypeChange()
            Dim src1 = "
Imports System
Class C
    Sub F(a1 As Integer, a2 As Integer)
        Dim f2 = New Func(Of Integer, Integer)(Function(a3) a1 + a2)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub F(a1 As Byte)
        Dim f2 = New Func(Of Integer, Integer)(Function(a3) a1)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
            {
                SemanticEdit(SemanticEditKind.Delete, Function(c) c.GetMember("C.F"), deletedSymbolContainerProvider:=Function(c) c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Insert, Function(c) c.GetMember("C.F"))
            },
            capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Update_CeaseCapture_MethodParameter_LocalToParameter()
            Dim src1 = "
Imports System
Class C
    Sub F(a1 As Integer)
        Dim a2 As Integer
        Dim f2 = New Func(Of Integer, Integer)(Function(a3) a1 + a2)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub F(a1 As Integer, a2 As Integer)
        Dim f2 = New Func(Of Integer, Integer)(Function(a3) a1 + a2)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                {
                    SemanticEdit(SemanticEditKind.Delete, Function(c) c.GetMember("C.F"), deletedSymbolContainerProvider:=Function(c) c.GetMember("C")),
                    SemanticEdit(SemanticEditKind.Insert, Function(c) c.GetMember("C.F"))
                },
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Update_CeaseCapture_MethodParameter_ParameterToLocal()
            Dim src1 = "
Imports System
Class C
    Sub F(a1 As Integer, a2 As Integer)
        Dim f2 = New Func(Of Integer, Integer)(Function(a3) a1 + a2)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub F(a1 As Integer)
        Dim a2 As Integer
        Dim f2 = New Func(Of Integer, Integer)(Function(a3) a1 + a2)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                {
                    SemanticEdit(SemanticEditKind.Delete, Function(c) c.GetMember("C.F"), deletedSymbolContainerProvider:=Function(c) c.GetMember("C")),
                    SemanticEdit(SemanticEditKind.Insert, Function(c) c.GetMember("C.F"))
                },
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/1290")>
        Public Sub Lambdas_Update_CeaseCapture_LambdaParameter1()
            Dim src1 = "
Imports System
Class C
    Sub F()
        Dim f1 = New Func(Of Integer, Integer, Integer)(
            Function(a1, a2)
                Dim f2 = New Func(Of Integer, Integer)(Function(a3) a1 + a2)
                Return a1
            End Function)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub F()
        Dim f1 = New Func(Of Integer, Integer, Integer)(
            Function(a1, a2)
                Dim f2 = New Func(Of Integer, Integer)(Function(a3) a2) 
                Return a1
            End Function)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact, WorkItem("https://devdiv.visualstudio.com/DevDiv/_workitems?id=234448")>
        Public Sub Lambdas_Update_CeaseCapture_SetterValueParameter1()
            Dim src1 = "
Imports System

Class C
    Property D As Integer
        Get
            Return 0
        End Get

        Set
            Call New Action(Sub() Console.Write(value)).Invoke()
        End Set
    End Property
End Class
"
            Dim src2 = "
Imports System

Class C
    Property D As Integer
        Get
            Return 0
        End Get

        Set
        End Set
    End Property
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.set_D"))})
        End Sub

        <Fact, WorkItem("https://devdiv.visualstudio.com/DevDiv/_workitems?id=234448")>
        Public Sub Lambdas_Update_CeaseCapture_IndexerSetterValueParameter1()
            Dim src1 = "
Imports System

Class C
    Property D(a1 As Integer, a2 As Integer) As Integer
        Get
            Return 0
        End Get

        Set
            Call New Action(Sub() Console.Write(value)).Invoke()
        End Set
    End Property
End Class
"
            Dim src2 = "
Imports System

Class C
    Property D(a1 As Integer, a2 As Integer) As Integer
        Get
            Return 0
        End Get

        Set
        End Set
    End Property
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.set_D"))})
        End Sub

        <Fact, WorkItem("https://devdiv.visualstudio.com/DevDiv/_workitems?id=234448")>
        Public Sub Lambdas_Update_CeaseCapture_IndexerSetterValueParameter2()
            Dim src1 = "
Imports System

Class C
    Property D As Integer
        Get
            Return 0
        End Get

        Set(Value As Integer)
            Call New Action(Sub() Console.Write(value)).Invoke()
        End Set
    End Property
End Class
"
            Dim src2 = "
Imports System

Class C
    Property D As Integer
        Get
            Return 0
        End Get

        Set(Value As Integer)
        End Set
    End Property
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.set_D"))})
        End Sub

        <Fact, WorkItem("https://devdiv.visualstudio.com/DevDiv/_workitems?id=234448")>
        Public Sub Lambdas_Update_CeaseCapture_EventAdderValueParameter1()
            Dim src1 = "
Imports System

Class C
    Custom Event D As Action
        AddHandler(Value As Action)
            Call New Action(Sub() Console.Write(Value)).Invoke()
        End AddHandler

        RemoveHandler(Value As Action)
        End RemoveHandler

        RaiseEvent()
        End RaiseEvent
    End Event
End Class
"
            Dim src2 = "
Imports System

Class C
    Custom Event D As Action
        AddHandler(Value As Action)
        End AddHandler

        RemoveHandler(Value As Action)
        End RemoveHandler

        RaiseEvent()
        End RaiseEvent
    End Event
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.add_D")))
        End Sub

        <Fact, WorkItem("https://devdiv.visualstudio.com/DevDiv/_workitems?id=234448")>
        Public Sub Lambdas_Update_CeaseCapture_EventRemoverValueParameter1()
            Dim src1 = "
Imports System

Class C
    Custom Event D As Action
        AddHandler(Value As Action)
        End AddHandler

        RemoveHandler(Value As Action)
            Call New Action(Sub() Console.Write(value)).Invoke()
        End RemoveHandler

        RaiseEvent()
        End RaiseEvent
    End Event
End Class
"
            Dim src2 = "
Imports System

Class C
    Custom Event D As Action
        AddHandler(Value As Action)
        End AddHandler

        RemoveHandler(Value As Action)
        End RemoveHandler

        RaiseEvent()
        End RaiseEvent
    End Event
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.remove_D"))})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_DeleteCapture1()
            Dim src1 = "
Imports System
Class C
    Sub F()
        Dim y As Integer = 1
        Dim f1 = New Func(Of Integer, Integer)(
            Function(a1)
                Dim f2 = New Func(Of Integer, Integer)(Function(a2) y + a2)
                Return y
            End Function)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub F()
        Dim f1 = New Func(Of Integer, Integer)(Function(a1)
                Dim f2 = New Func(Of Integer, Integer)(Function(a2) a2)
                Return a1
            End Function)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Capturing_For_EachFor_Using()
            Dim src1 = "
Imports System
Imports System.IO

Class C
    Public Sub F()
        For Each a As Integer In {1}
        Next

        Using b As New MemoryStream()
        End Using

        For c As Integer = 0 To 1
        Next
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.IO

Class C
    Public Sub F()
        For Each a As Integer In {1}
            Dim x = New Action(Sub() Console.WriteLine(a))
        Next

        Using b As New MemoryStream()
            Dim x = New Action(Sub() Console.WriteLine(b))
        End Using

        For c As Integer = 0 To 1
            Dim x = New Action(Sub() Console.WriteLine(c))
        Next
    End Sub
End Class

"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                {SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)},
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Capturing_IndexerGetterParameter2()
            Dim src1 = "
Imports System
Class C
    Readonly Property Item(a1 As Integer, a2 As Integer) As Func(Of Integer, Integer)
        Get
            Return New Func(Of Integer, Integer)(Function(a3) a2)
        End Get
    End Property
End Class
"
            Dim src2 = "
Imports System
Class C
    Readonly Property Item(a1 As Integer, a2 As Integer) As Func(Of Integer, Integer)
        Get
            Return New Func(Of Integer, Integer)(Function(a3) a1 + a2)
        End Get
    End Property
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.get_Item"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Capturing_IndexerGetterParameter_ParameterInsert()
            Dim src1 = "
Imports System
Class C
    Readonly Property Item(a1 As Integer) As Func(Of Integer, Integer)
        Get
            Return New Func(Of Integer, Integer)(Function(a3) a1)
        End Get
    End Property
End Class
"
            Dim src2 = "
Imports System
Class C
    Readonly Property Item(a1 As Integer, a2 As Integer) As Func(Of Integer, Integer)
        Get
            Return New Func(Of Integer, Integer)(Function(a3) a1 + a2)
        End Get
    End Property
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
            {
                SemanticEdit(SemanticEditKind.Delete, Function(c) c.GetMember("C.Item"), deletedSymbolContainerProvider:=Function(c) c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Delete, Function(c) c.GetMember("C.get_Item"), deletedSymbolContainerProvider:=Function(c) c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Insert, Function(c) c.GetMember("C.Item")),
                SemanticEdit(SemanticEditKind.Insert, Function(c) c.GetMember("C.get_Item"))
            }, capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Capturing_IndexerSetterParameter1()
            Dim src1 = "
Imports System
Class C
    Property Item(a1 As Integer, a2 As Integer) As Func(Of Integer, Integer)
        Get
            Return Nothing
        End Get
        Set
            Dim f = New Func(Of Integer, Integer)(Function(a3) a2)
        End Set
    End Property
End Class
"
            Dim src2 = "
Imports System
Class C
    Property Item(a1 As Integer, a2 As Integer) As Func(Of Integer, Integer)
        Get
            Return Nothing
        End Get
        Set
            Dim f = New Func(Of Integer, Integer)(Function(a3) a1 + a2)
        End Set
    End Property
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                {SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.set_Item"), preserveLocalVariables:=True)},
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Capturing_SetterValueParameter1()
            Dim src1 = "
Imports System

Class C
    Property D As Integer
        Get
            Return 0
        End Get

        Set
        End Set
    End Property
End Class
"
            Dim src2 = "
Imports System

Class C
    Property D As Integer
        Get
            Return 0
        End Get

        Set
            Call New Action(Sub() Console.Write(value)).Invoke()
        End Set
    End Property
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                {SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.set_D"), preserveLocalVariables:=True)},
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Capturing_IndexerSetterValueParameter1()
            Dim src1 = "
Imports System

Class C
    Property D(a1 As Integer, a2 As Integer) As Integer
        Get
            Return 0
        End Get

        Set
        End Set
    End Property
End Class
"
            Dim src2 = "
Imports System

Class C
    Property D(a1 As Integer, a2 As Integer) As Integer
        Get
            Return 0
        End Get

        Set
            Call New Action(Sub() Console.Write(value)).Invoke()
        End Set
    End Property
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                {SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.set_D"), preserveLocalVariables:=True)},
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Capturing_IndexerSetterValueParameter2()
            Dim src1 = "
Imports System

Class C
    Property D As Integer
        Get
            Return 0
        End Get

        Set(value As Integer)
        End Set
    End Property
End Class
"
            Dim src2 = "
Imports System

Class C
    Property D As Integer
        Get
            Return 0
        End Get

        Set(value As Integer)
            Call New Action(Sub() Console.Write(value)).Invoke()
        End Set
    End Property
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                {SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.set_D"), preserveLocalVariables:=True)},
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Capturing_EventAdderValueParameter1()
            Dim src1 = "
Imports System

Class C
    Custom Event D As Action
        AddHandler(value As Action)
        End AddHandler

        RemoveHandler(value As Action)
        End RemoveHandler

        RaiseEvent()
        End RaiseEvent
    End Event
End Class
"
            Dim src2 = "
Imports System

Class C
    Custom Event D As Action
        AddHandler(value As Action)
            Call New Action(Sub() Console.Write(value)).Invoke()
        End AddHandler

        RemoveHandler(value As Action)
        End RemoveHandler

        RaiseEvent()
        End RaiseEvent
    End Event
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                {SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.add_D"), preserveLocalVariables:=True)},
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Capturing_EventRemoverValueParameter1()
            Dim src1 = "
Imports System

Class C
    Custom Event D As Action
        AddHandler(value As Action)
        End AddHandler

        RemoveHandler(value As Action)
        End RemoveHandler

        RaiseEvent()
        End RaiseEvent
    End Event
End Class
"
            Dim src2 = "
Imports System

Class C
    Custom Event D As Action
        AddHandler(value As Action)
        End AddHandler

        RemoveHandler(value As Action)
            Call New Action(Sub() Console.Write(value)).Invoke()
        End RemoveHandler

        RaiseEvent()
        End RaiseEvent
    End Event
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                {SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.remove_D"), preserveLocalVariables:=True)},
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Capturing_MethodParameter1()
            Dim src1 = "
Imports System
Class C
    Sub F(a1 As Integer, a2 As Integer)
        Dim f2 = New Func(Of Integer, Integer)(Function(a3) a1)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub F(a1 As Integer, a2 As Integer)
        Dim f2 = New Func(Of Integer, Integer)(Function(a3) a1 + a2)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Capturing_MethodParameter_ParameterInsert()
            Dim src1 = "
Imports System
Class C
    Sub F(a1 As Integer)
        Dim f2 = New Func(Of Integer, Integer)(Function(a3) a1)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub F(a1 As Integer, a2 As Integer)
        Dim f2 = New Func(Of Integer, Integer)(Function(a3) a1 + a2)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                {
                    SemanticEdit(SemanticEditKind.Delete, Function(c) c.GetMember("C.F"), deletedSymbolContainerProvider:=Function(c) c.GetMember("C")),
                    SemanticEdit(SemanticEditKind.Insert, Function(c) c.GetMember("C.F"))
                },
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Capturing_MethodParameter_ParameterInsert_Partial()
            Dim src1 = "
Imports System

Partial Public Class C
    Partial Private Sub F(a1 As Integer)
    End Sub    
End Class

Partial Public Class C
    Private Sub F(a1 As Integer)
        Dim x = New Func(Of Integer, Integer)(Function(a3) a1)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Partial Public Class C
    Partial Private Sub F(a1 As Integer, a2 As Integer)
    End Sub    
End Class

Partial Public Class C
    Private Sub F(a1 As Integer, a2 As Integer)
        Dim x = New Func(Of Integer, Integer)(Function(a3) a1 + a2)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                {
                    SemanticEdit(SemanticEditKind.Delete, Function(c) c.GetMember(Of MethodSymbol)("C.F").PartialImplementationPart, deletedSymbolContainerProvider:=Function(c) c.GetMember("C"), partialType:="C"),
                    SemanticEdit(SemanticEditKind.Insert, Function(c) c.GetMember(Of MethodSymbol)("C.F").PartialImplementationPart, partialType:="C")
                },
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType Or EditAndContinueCapabilities.NewTypeDefinition)
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Capturing_LambdaParameter1()
            Dim src1 = "
Imports System
Class C
    Sub F()
        Dim f1 = New Func(Of Integer, Integer, Integer)(
            Function(a1, a2)
                Dim f2 = New Func(Of Integer, Integer)(Function(a3) a2)
                Return a1
            End Function)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub F()
        Dim f1 = New Func(Of Integer, Integer, Integer)(
            Function(a1, a2)
                Dim f2 = New Func(Of Integer, Integer)(Function(a3) a1 + a2)
                Return a1
            End Function)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_StaticToThisOnly1()
            Dim src1 = "
Imports System
Class C
    Dim x As Integer = 1

    Sub F()
        Dim f = New Func(Of Integer, Integer)(Function(a) a)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Dim x As Integer = 1

    Sub F()
        Dim f = New Func(Of Integer, Integer)(Function(a) a + x)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_StaticToThisOnly_Partial()
            Dim src1 = "
Imports System

Partial Class C
    Dim x As Integer = 1
    Private Partial Sub F() ' def
    End Sub
End Class

Partial Class C
    Private Sub F() ' impl
        Dim f = New Func(Of Integer, Integer)(Function(a) a)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Partial Class C
    Dim x As Integer = 1
    Private Partial Sub F() ' def
    End Sub
End Class

Partial Class C
    Private Sub F() ' impl
        Dim f = New Func(Of Integer, Integer)(Function(a) a + x)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember(Of MethodSymbol)("C.F").PartialImplementationPart, preserveLocalVariables:=True, partialType:="C")})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_StaticToThisOnly3()
            Dim src1 = "
Imports System
Class C
    Dim x As Integer = 1

    Sub F()
        Dim f1 = New Func(Of Integer, Integer)(Function(a1) a1)
        Dim f2 = New Func(Of Integer, Integer)(Function(a2) a2 + x)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Dim x As Integer = 1

    Sub F()
        Dim f1 = New Func(Of Integer, Integer)(Function(a1) a1 + x)
        Dim f2 = New Func(Of Integer, Integer)(Function(a2) a2 + x)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_StaticToClosure1()
            Dim src1 = "
Imports System
Class C
    Sub F()
        Dim x As Integer = 1
        Dim f1 = New Func(Of Integer, Integer)(Function(a1) a1)
        Dim f2 = New Func(Of Integer, Integer)(Function(a2) a2 + x)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub F()
        Dim x As Integer = 1
        Dim f1 = New Func(Of Integer, Integer)(
            Function(a1)
                Return a1 +
                        x + ' 1
                        x   ' 2
            End Function)

        Dim f2 = New Func(Of Integer, Integer)(Function(a2) a2 + x)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_ThisOnlyToClosure1()
            Dim src1 = "
Imports System
Class C
    Dim x As Integer = 1

    Sub F()
        Dim y As Integer = 1
        Dim f1 = New Func(Of Integer, Integer)(Function(a1) a1 + x)
        Dim f2 = New Func(Of Integer, Integer)(Function(a2) a2 + x + y)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Dim x As Integer = 1

    Sub F()
        Dim y As Integer = 1
        Dim f1 = New Func(Of Integer, Integer)(Function(a1) a1 + x + y)
        Dim f2 = New Func(Of Integer, Integer)(Function(a2) a2 + x + y)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Nested1()
            Dim src1 = "
Imports System
Class C
    Sub F()
        Dim y As Integer = 1
        Dim f1 = New Func(Of Integer, Integer)(
            Function(a1)
                Dim f2 = New Func(Of Integer, Integer)(Function(a2) a2 + y)
                Return a1
            End Function)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C

    Sub F()
        Dim y As Integer = 1
        Dim f1 = New Func(Of Integer, Integer)(
            Function(a1)
                Dim f2 = New Func(Of Integer, Integer)(Function(a2) a2 + y)
                Return a1 + y
            End Function)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Nested2()
            Dim src1 = "
Imports System
Class C
    Sub F()
        Dim y As Integer = 1
        Dim f1 = New Func(Of Integer, Integer)(
            Function(a1)
                Dim f2 = New Func(Of Integer, Integer)(Function(a2) a2)
                Return a1
            End Function)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub F()
        Dim y As Integer = 1
        Dim f1 = New Func(Of Integer, Integer)(
            Function(a1)
                Dim f2 = New Func(Of Integer, Integer)(Function(a2) a1 + a2)
                Return a1
            End Function)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Accessing_Closure1()
            Dim src1 = "
Imports System
Class C
    Sub G(f As Func(Of Integer, Integer))
    End Sub

    Sub F()
        Dim x0 As Integer = 0, y0 As Integer = 0
        G(Function(a) x0)
        G(Function(a) y0)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub G(f As Func(Of Integer, Integer))
    End Sub

    Sub F()
        Dim x0 As Integer = 0, y0 As Integer = 0
        G(Function(a) x0)
        G(Function(a) y0 + x0)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Accessing_Closure2()
            Dim src1 = "
Imports System
Class C
    Sub G(f As Func(Of Integer, Integer))
    End Sub

    Dim x As Integer = 0                                    ' Group #0

    Sub F()
        Do
            Dim x0 As Integer = 0, y0 As Integer = 0        ' Group #0
            Do
                Dim x1 As Integer = 0, y1 As Integer = 0    ' Group #1

                G(Function(a) x + x0)
                G(Function(a) x0)
                G(Function(a) y0)
                G(Function(a) x1)
            Loop
        Loop
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub G(f As Func(Of Integer, Integer))
    End Sub

    Dim x As Integer = 0                                    ' Group #0

    Sub F()
        Do
            Dim x0 As Integer = 0, y0 As Integer = 0        ' Group #0
            Do
                Dim x1 As Integer = 0, y1 As Integer = 0    ' Group #1

                G(Function(a) x)  ' error: disconnecting previously connected closures
                G(Function(a) x0)
                G(Function(a) y0)
                G(Function(a) x1)
            Loop
        Loop
    End Sub
End Class"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Accessing_Closure3()
            Dim src1 = "
Imports System
Class C
    Sub G(f As Func(Of Integer, Integer))
    End Sub

    Dim x As Integer = 0                                        ' Group #0

    Sub F()
        Do
            Dim x0 As Integer = 0, y0 As Integer = 0            ' Group #0
            Do                                                  
                Dim x1 As Integer = 0, y1 As Integer = 0        ' Group #1
                
                G(Function(a) x)
                G(Function(a) x0)
                G(Function(a) y0)
                G(Function(a) x1)
                G(Function(a) y1)
            Loop
        Loop
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub G(f As Func(Of Integer, Integer))
    End Sub

    Dim x As Integer = 0                                        ' Group #0

    Sub F()
        Do
            Dim x0 As Integer = 0, y0 As Integer = 0            ' Group #0
            Do                                                  
                Dim x1 As Integer = 0, y1 As Integer = 0        ' Group #1
                
                G(Function(a) x)
                G(Function(a) x0)
                G(Function(a) y0)
                G(Function(a) x1)
                G(Function(a) y1 + x0) ' error: connecting previously disconnected closures
            Loop
        Loop
    End Sub
End Class"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_Update_Accessing_Closure4()
            Dim src1 = "
Imports System
Class C
    Sub G(f As Func(Of Integer, Integer))
    End Sub

    Dim x As Integer = 0                                    ' Group #0
    
    Sub F()
        Do
            Dim x0 As Integer = 0, y0 As Integer = 0        ' Group #0
            Do                                              
                Dim x1 As Integer = 0, y1 As Integer = 0    ' Group #1

                G(Function(a) x + x0)
                G(Function(a) x0)
                G(Function(a) y0)
                G(Function(a) x1)
                G(Function(a) y1)
            Loop
        Loop
    End Sub
End Class
"
            Dim src2 = "
Imports System
Class C
    Sub G(f As Func(Of Integer, Integer))
    End Sub

    Dim x As Integer = 0                                    ' Group #0

    Sub F()
        Do
            Dim x0 As Integer = 0, y0 As Integer = 0        ' Group #0  
            Do
                Dim x1 As Integer = 0, y1 As Integer = 0    ' Group #1

                G(Function(a) x)       ' error: disconnecting previously connected closures
                G(Function(a) x0)      
                G(Function(a) y0)      
                G(Function(a) x1)      
                G(Function(a) y1 + x0) ' error: connecting previously disconnected closures
            Loop
        Loop
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Lambdas_CapturedLocal_Rename_CaseChange()
            Dim src1 = "
Imports System

Class C
    <N:0>Shared Sub F()
        Dim x As Integer = 1
        Dim f As Func(Of Integer) = Function() x
    End Sub</N:0>
End Class
"
            Dim src2 = "
Imports System

Class C
    <N:0>Shared Sub F()
        Dim <S:0>X</S:0> As Integer = 1
        Dim f As Func(Of Integer) = Function() X
    End Sub</N:0>
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            ' Note that lifted variable is a field, which can't be renamed
            edits.VerifySemantics(
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.RenamingCapturedVariable, syntaxMap.Position(0), {"x", "X"})
                }))
        End Sub

        <Fact>
        Public Sub Lambdas_CapturedLocal_Rename()
            Dim src1 = "
Imports System

Class C
    <N:0>Shared Sub F()
        Dim x As Integer = 1
        Dim f As Func(Of Integer) = Function() x
    End Sub</N:0>
End Class
"
            Dim src2 = "
Imports System

Class C
    <N:0>Shared Sub F()
        Dim <S:0>y</S:0> As Integer = 1
        Dim f As Func(Of Integer) = Function() y
    End Sub</N:0>
End Class
"

            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.RenamingCapturedVariable, syntaxMap.Position(0), {"x", "y"})
                }))
        End Sub

        <Fact>
        Public Sub Lambdas_CapturedLocal_ChangeType()
            Dim src1 = "
Imports System

Class C
    <N:0>Shared Sub F()
        Dim x As Integer = 1
        Dim f As Func(Of Integer) = <N:1>Function() x</N:1>
    End Sub</N:0>
End Class
"
            Dim src2 = "
Imports System

Class C
    <N:0>Shared Sub F()
        Dim <S:0>x</S:0> As Byte = 1
        Dim f As Func(Of Integer) = <N:1>Function() x</N:1>
    End Sub</N:0>
End Class
"

            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
                SemanticEdit(
                    SemanticEditKind.Update,
                    Function(c) c.GetMember("C.F"),
                    syntaxMap,
                    rudeEdits:={RuntimeRudeEdit(marker:=0, RudeEditKind.ChangingCapturedVariableType, syntaxMap.Position(0), {"x", "Integer"})}))
        End Sub

        <Fact>
        Public Sub Lambdas_CapturedParameter_Rename()
            Dim src1 = "
Imports System

Class C
    <N:0>Shared Sub F(x As Integer)
        Dim f As Func(Of Integer) = Function() x
    End Sub</N:0>
End Class
"
            Dim src2 = "
Imports System

Class C
    <N:0>Shared Sub F(<S:0>y</S:0> As Integer)
        Dim f As Func(Of Integer) = Function() y
    End Sub</N:0>
End Class
"

            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
            {
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.RenamingCapturedVariable, syntaxMap.Position(0), {"x", "y"})
                })
            },
            capabilities:=EditAndContinueCapabilities.UpdateParameters)
        End Sub

        <Fact>
        Public Sub Lambdas_CapturedParameter_Rename_Lambda_MultiLine()
            Dim src1 = "
Imports System

Class C
    <N:0>Shared Sub F(x As Integer)
        Dim f1 = <N:1>Function(x)
                    Dim f2 = <N:2>Function() x</N:2>
                 End Function</N:1>
    End Sub</N:0>
End Class
"
            Dim src2 = "
Imports System

Class C
    <N:0>Shared Sub F(x As Integer)
        Dim f1 = <N:1>Function(<S:0>y</S:0>)
                    Dim f2 = <N:2>Function() y</N:2>
                 End Function</N:1>
    End Sub</N:0>
End Class
"

            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
            {
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(1, RudeEditKind.RenamingCapturedVariable, syntaxMap.Position(0), {"x", "y"})
                })
            },
            capabilities:=EditAndContinueCapabilities.UpdateParameters)
        End Sub

        <Fact>
        Public Sub Lambdas_CapturedParameter_Rename_Lambda_SingleLine()
            Dim src1 = "
Imports System

Class C
    Shared Function G(a As Func(Of Integer)) As Integer
        Return 0
    End Function

    <N:0>Shared Sub F(x As Integer)
        Dim f1 = <N:1>Function(x) G(<N:2>Function() x</N:2>)</N:1>
    End Sub</N:0>
End Class
"
            Dim src2 = "
Imports System

Class C
    Shared Function G(a As Func(Of Integer)) As Integer
        Return 0
    End Function

    <N:0>Shared Sub F(x As Integer)
        Dim f1 = <N:1>Function(<S:0>y</S:0>) G(<N:2>Function() y</N:2>)</N:1>
    End Sub</N:0>
End Class
"

            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
            {
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(1, RudeEditKind.RenamingCapturedVariable, syntaxMap.Position(0), {"x", "y"})
                })
            },
            capabilities:=EditAndContinueCapabilities.UpdateParameters)
        End Sub

        <Fact>
        Public Sub Lambdas_CapturedParameter_Rename_ConstructorDeclaration()
            Dim src1 = "
Imports System

Class C
    <N:0>Sub New(x As Integer)
        Dim f As Func(Of Integer) = Function() x
    End Sub</N:0>
End Class
"
            Dim src2 = "
Imports System

Class C
    <N:0>Sub New(<S:0>y</S:0> As Integer)
        Dim f As Func(Of Integer) = Function() y
    End Sub</N:0>
End Class
"

            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
            {
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C..ctor"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.RenamingCapturedVariable, syntaxMap.Position(0), {"x", "y"})
                })
            },
            capabilities:=EditAndContinueCapabilities.UpdateParameters)
        End Sub

        <Fact>
        <WorkItem("https://github.com/dotnet/roslyn/issues/68708")>
        Public Sub Lambdas_CapturedParameter_ChangeType()
            Dim src1 = "
Imports System

Class C
    Shared Sub F(x As Integer)
        Dim f As Func(Of Integer) = Function() x
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C
    Shared Sub F(x As Byte)
        Dim f As Func(Of Integer) = Function() x
    End Sub
End Class
"

            Dim edits = GetTopEdits(src1, src2)

            edits.VerifySemantics(
                {
                    SemanticEdit(SemanticEditKind.Delete, Function(c) c.GetMember("C.F"), deletedSymbolContainerProvider:=Function(c) c.GetMember("C")),
                    SemanticEdit(SemanticEditKind.Insert, Function(c) c.GetMember("C.F"))
                },
                capabilities:=EditAndContinueCapabilities.AddMethodToExistingType)
        End Sub

        <Fact>
        Public Sub Lambdas_Signature_MatchingErrorType()
            Dim src1 = "
Imports System

Class C

    Sub G(f As Func(Of Unknown, Unknown))
    End Sub

    Sub F()
        G(Function(a) 1)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C

    Sub G(f As Func(Of Unknown, Unknown))
    End Sub

    Sub F()
        G(Function(a) 2)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                ActiveStatementsDescription.Empty,
                semanticEdits:=
                {
                    SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember(Of NamedTypeSymbol)("C").GetMembers("F").Single(), preserveLocalVariables:=True)
                })
        End Sub

        <Fact>
        Public Sub Lambdas_Signature_NonMatchingErrorType()
            Dim src1 = "
Imports System

Class C

    Sub G1(f As Func(Of Unknown1, Unknown1))
    End Sub

    Sub G2(f As Func(Of Unknown2, Unknown2))
    End Sub

    Sub F()
        G1(<N:0>Function(a) 1</N:0>)
    End Sub
End Class
"
            Dim src2 = "
Imports System

Class C

    Sub G1(f As Func(Of Unknown1, Unknown1))
    End Sub

    Sub G2(f As Func(Of Unknown2, Unknown2))
    End Sub

    Sub F()
        G2(<N:0>Function(a) 2</N:0>)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.NodePosition(0), {GetResource("lambda")})
                }))
        End Sub

#End Region

#Region "Queries"
        <Fact>
        Public Sub Queries_Update_Signature_Select1()
            Dim src1 = "
Imports System
Imports System.Linq

Class C
    Sub F()
        Dim result = From a In {1} <N:0>Select a</N:0>
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq

Class C
    Sub F()
        Dim result = From a In {1.0} <N:0>Select a</N:0>
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
            {
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), {GetResource("select clause")})
                })
            })
        End Sub

        <Fact>
        Public Sub Queries_Update_Signature_Select2()
            Dim src1 = "
Imports System
Imports System.Linq

Class C
    Sub F()
        Dim result = From a In {1} <N:0>Select b = a</N:0>
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq

Class C
    Sub F()
        Dim result = From a In {1.0} <N:0>Select b = a.ToString()</N:0>
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
            {
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), {GetResource("select clause")})
                })
            })
        End Sub

        <Fact>
        Public Sub Queries_Update_Signature_Select3()
            Dim src1 = "
Imports System
Imports System.Linq

Class C
    Sub F()
        Dim result = From a In {1} <N:0>Select b = a, c = a</N:0>
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq

Class C
    Sub F()
        Dim result = From a In {1.0} <N:0>Select b = a, c = a.ToString()</N:0>
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
            {
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), {GetResource("select clause")})
                })
            })
        End Sub

        <Fact>
        Public Sub Queries_Update_Signature_From1()
            Dim src1 = "
Imports System
Imports System.Linq

Class C
    Sub F()
        Dim result = From a In {1} From <N:0>b In {2}</N:0> Select b
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq

Class C
    Sub F()
        Dim result = From a In {1.0} <S:0>From</S:0> <N:0>b In {2}</N:0> Select b
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
            {
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.Position(0), {GetResource("from clause")})
                })
            })
        End Sub

        <Fact>
        Public Sub Queries_Update_Signature_From2()
            Dim src1 = "
Imports System
Imports System.Linq

Class C
    Sub F()
        Dim result = From a As Long In {1} From b In {2} Select b
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq

Class C
    Sub F()
        Dim result = From a As System.Int64 In {1} From b In {2} Select b
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact>
        Public Sub Queries_Update_Signature_From3()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} From b In {2} Select b
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In New List(Of Integer)() From b In New List(Of Integer)() Select b
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_FromInAggregate1()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = Aggregate a In {1} From b in {2}, c in {3} Into Count()
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = Aggregate a In {1.0} From b in {2}, c in {3} Into Count()
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)

            ' no lambdas created
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "From", "From clause"))
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_FromInAggregate2()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = Aggregate a In {1} From b in {2}, c in {3} Into Count()
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = Aggregate a In {1} From b in {2.0}, c in {3} Into Count()
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)

            ' no lambdas created
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "From", "From clause"))
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_FromInAggregate3()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = Aggregate a In {1} From b in {2}, c in {3} Into Count()
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = Aggregate a In {1} From b in {2}, c in {3.0} Into Count()
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)

            ' no lambdas created
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "From", "From clause"))
        End Sub

        <Fact>
        Public Sub Queries_Update_Signature_Let1()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Let <N:0>b = 1</N:0> Select a
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} <S:0>Let</S:0> <N:0>b = 1.0</N:0> Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
            {
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.Position(0), {GetResource("let clause")})
                })
            })
        End Sub

        <Fact>
        Public Sub Queries_Update_Signature_OrderBy1()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Order By <N:0>a + 1 Descending</N:0>, a + 2 Ascending Select a
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Order By <N:0>a + 1.0 Descending</N:0>, a + 2 Ascending Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
            {
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), {GetResource("orderby clause")})
                })
            })
        End Sub

        <Fact>
        Public Sub Queries_Update_Signature_OrderBy2()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Order By a + 1 Descending, <N:0>a + 2 Ascending</N:0> Select a
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Order By a + 1 Descending, <N:0>a + 2.0 Ascending</N:0> Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            Dim syntaxMap = edits.GetSyntaxMap()

            edits.VerifySemantics(
            {
                SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), syntaxMap, rudeEdits:=
                {
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), {GetResource("orderby clause")})
                })
            })
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_Join1()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Join b In {2} On a Equals b Select a
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Join b In {2.0} On a Equals b Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "Join", "Join clause"))
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_Join2()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Join b In {2} On a Equals b Select a
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Join b In {2} On a + 1.0 Equals b Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "Join", "Join clause"))
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_Join3()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Join b In {2} On a Equals b Select a
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Join b In {2} On a Equals b + 1.0 Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "Join", "Join clause"))
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_Join4()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Join b1 In {2} Join b2 In {2} On b1 Equals b2 On b1 Equals a Select a
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Join b1 In {2} Join b2 In {2.0} On b1 Equals b2 On b1 Equals a Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "Join", "Join clause"))
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_Join5()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Join b1 In {2} Join b2 In {2} On b1 Equals b2 On b1 Equals a Select a
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Join b1 In {2} Join b2 In {2} On b1 + 1.0 Equals b2 On b1 Equals a Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "Join", "Join clause"))
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_Join()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Join b1 In {2} Join b2 In {2} On b1 Equals b2 On b1 Equals a Select a
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Join b1 In {2} Join b2 In {2} On b1 Equals b2 + 1.0 On b1 Equals a Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "Join", "Join clause"))
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_GroupJoin1()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Group Join b1 In {2} Join b2 In {2} On b1 Equals b2 On b1 Equals a Into Count(a) Select a
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Group Join b1 In {2.0} Join b2 In {2} On b1 Equals b2 On b1 Equals a Into Count(a) Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "Group Join", "Group Join clause"))
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_GroupJoin2()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Group Join b1 In {2} Join b2 In {2} On b1 Equals b2 On b1 Equals a Into Count(a) Select a
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Group Join b1 In {2} Join b2 In {2.0} On b1 Equals b2 On b1 Equals a Into Count(a) Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "Group Join", "Group Join clause"))
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_GroupJoin3()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Group Join b1 In {2} Join b2 In {2} On b1 Equals b2 On b1 Equals a Into Count(a) Select a
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Group Join b1 In {2} Join b2 In {2} On b1 + 1.0 Equals b2 On b1 Equals a Into Count(a) Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "Group Join", "Group Join clause"))
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_GroupJoin4()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Group Join b1 In {2} Join b2 In {2} On b1 Equals b2 On b1 Equals a Into Count(a) Select a
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Group Join b1 In {2} Join b2 In {2} On b1 Equals b2 On b1 Equals a Into Count(a + 1.0) Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)

            ' no change in type
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_GroupBy1()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result1 = From a In {1} Group x = 1, y = 2 By z = a, u = a Into Group Select Group
        Dim result2 = From a In {1} Group x = 1, y = 2 By z = a, u = a Into Group Select Group
        Dim result3 = From a In {1} Group x = 1, y = 2 By z = a, u = a Into Group Select Group
        Dim result4 = From a In {1} Group x = 1, y = 2 By z = a, u = a Into Group Select Group
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result1 = From a In {1} Group x = 1.0, y = 2 By z = a, u = a Into Group Select Group
        Dim result2 = From a In {1} Group x = 1, y = 2.0 By z = a, u = a Into Group Select Group
        Dim result3 = From a In {1} Group x = 1, y = 2 By z = a + 1.0, u = a Into Group Select Group
        Dim result4 = From a In {1} Group x = 1, y = 2 By z = a, u = a + 1.0 Into Group Select Group
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "Group", "Group By clause"),
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "Group", "Group By clause"),
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "Group", "Group By clause"),
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "Group", "Group By clause"))
        End Sub

        <Fact>
        Public Sub Queries_Update_Signature_Partition1()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result1 = From a In {1} Take 1 Select a
        Dim result2 = From a In {1} Skip 1 Select a
        Dim result3 = From a In {1} Take While a Select a
        Dim result4 = From a In {1} Skip While a Select a
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result1 = From a In {1} Take 1.0 Select a
        Dim result2 = From a In {1} Skip 1.0 Select a
        Dim result3 = From a In {1} Take While a + 1.0 Select a
        Dim result4 = From a In {1} Skip While a + 1.0 Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)

            ' no change in lambda types
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_Aggregate1()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = Aggregate a In {1} Into Count()
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = Aggregate a In {1.0} Into Count()
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)

            ' no lambdas created
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_Aggregate2()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Aggregate b In {2} Into Count()
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Aggregate b In {2.0} Into Count()
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)

            ' change is in the aggregate lambda body, but not in its signature
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_Aggregate3()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Aggregate b In {2}, c In {3} Into Count()
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Aggregate b In {2}, c In {3.0} Into Count()
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "Aggregate", "Aggregate clause"))
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_Aggregate4()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Aggregate b In {2} Join c In {3} On c Equals b Into Count()
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = From a In {1} Aggregate b In {2} Join c In {3.0} On c Equals b Into Count()
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "Join", "Join clause"))
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_Update_Signature_Aggregate5()
            Dim src1 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = Aggregate b In {2} Select b Into Count()
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq
Imports System.Collections.Generic

Class C
    Sub F()
        Dim result = Aggregate b In {2} Select b + 1.0 Into Count()
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "Select", "Select clause"))
        End Sub

        <Fact>
        Public Sub Queries_FromSelect_Update()
            Dim src1 = "F(From a In b Select c)"
            Dim src2 = "F(From a In c Select c + 1)"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [a In b]@15 -> [a In c]@15",
                "Update [c]@29 -> [c + 1]@29")
        End Sub

        <Fact>
        Public Sub Queries_FromSelect_Delete()
            Dim src1 = "F(From a In b From c In d Select a + c)"
            Dim src2 = "F(From a In b Select c + 1)"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [a + c]@41 -> [c + 1]@29",
                "Delete [From c In d]@22",
                "Delete [c In d]@27",
                "Delete [c]@27")
        End Sub

        <Fact>
        Public Sub Queries_GroupBy_Update()
            Dim src1 = "F(From a In b Group a By a.x Into g Select g)"
            Dim src2 = "F(From a In b Group z By z.y Into h Select h)"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Update [a]@28 -> [z]@28",
                "Update [a.x]@33 -> [z.y]@33",
                "Update [g]@42 -> [h]@42",
                "Update [g]@51 -> [h]@51")
        End Sub

        <Fact>
        Public Sub Queries_OrderBy_Reorder()
            Dim src1 = "F(From a In b Order By a.x, a.b Descending, a.c Ascending Select a.d)"
            Dim src2 = "F(From a In b Order By a.x, a.c Ascending, a.b Descending Select a.d)"
            Dim edits = GetMethodEdits(src1, src2)

            edits.VerifyEdits(
                "Reorder [a.c Ascending]@52 -> @36")
        End Sub

        <Fact>
        Public Sub Queries_GroupJoin()
            Dim src1 = "F(From a1 In b1 Group Join c1 In d1 On e1 Equals f1 Into g1 = Group, h1 = Sum(f1) Select g1)"
            Dim src2 = "F(From a2 In b2 Group Join c2 In d2 On e2 Equals f2 Into g2 = Group, h2 = Sum(f2) Select g2)"
            Dim edits = GetMethodEdits(src1, src2)
            Dim actual = ToMatchingPairs(edits.Match)

            Dim expected = New MatchingPairs From
            {
                {"Sub F()", "Sub F()"},
                {"F(From a1 In b1 Group Join c1 In d1 On e1 Equals f1 Into g1 = Group, h1 = Sum(f1) Select g1)", "F(From a2 In b2 Group Join c2 In d2 On e2 Equals f2 Into g2 = Group, h2 = Sum(f2) Select g2)"},
                {"From a1 In b1 Group Join c1 In d1 On e1 Equals f1 Into g1 = Group, h1 = Sum(f1) Select g1", "From a2 In b2 Group Join c2 In d2 On e2 Equals f2 Into g2 = Group, h2 = Sum(f2) Select g2"},
                {"From a1 In b1", "From a2 In b2"},
                {"a1 In b1", "a2 In b2"},
                {"a1", "a2"},
                {"Group Join c1 In d1 On e1 Equals f1 Into g1 = Group, h1 = Sum(f1)", "Group Join c2 In d2 On e2 Equals f2 Into g2 = Group, h2 = Sum(f2)"},
                {"c1 In d1", "c2 In d2"},
                {"c1", "c2"},
                {"e1 Equals f1", "e2 Equals f2"},
                {"g1", "g2"},
                {"h1", "h2"},
                {"Sum(f1)", "Sum(f2)"},
                {"Select g1", "Select g2"},
                {"g1", "g2"},
                {"End Sub", "End Sub"}
            }

            expected.AssertEqual(actual)

            edits.VerifyEdits(
                "Update [a1 In b1]@15 -> [a2 In b2]@15",
                "Update [c1 In d1]@35 -> [c2 In d2]@35",
                "Update [e1 Equals f1]@47 -> [e2 Equals f2]@47",
                "Update [g1]@65 -> [g2]@65",
                "Update [h1]@77 -> [h2]@77",
                "Update [Sum(f1)]@82 -> [Sum(f2)]@82",
                "Update [g1]@97 -> [g2]@97",
                "Update [a1]@15 -> [a2]@15",
                "Update [c1]@35 -> [c2]@35")
        End Sub

        <Fact>
        Public Sub Queries_CapturedTransparentIdentifiers_FromClause1()
            Dim src1 As String = "
Imports System
Imports System.Linq

Class C

    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1} 
                     From b In {2} 
                     Where Z(Function() a) > 0 
                     Where Z(Function() b) > 0 
                     Where Z(Function() a) > 0 
                     Where Z(Function() b) > 0 
                     Select a
    End Sub
End Class
"

            Dim src2 As String = "
Imports System
Imports System.Linq

Class C

    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1}
                     From b In {2} 
                     Where Z(Function() a) > 1  ' update
                     Where Z(Function() b) > 2  ' update
                     Where Z(Function() a) > 3  ' update
                     Where Z(Function() b) > 4  ' update
                     Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact>
        Public Sub Queries_CapturedTransparentIdentifiers_LetClause1()
            Dim src1 As String = "
Imports System
Imports System.Linq

Class C
    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1} 
                     Let b = Z(Function() a) 
                     Select a + b
    End Sub
End Class
"
            Dim src2 As String = "
Imports System
Imports System.Linq

Class C
    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1} 
                     Let b = Z(Function() a) 
                     Select a - b
    End Sub
End Class"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics()
        End Sub

        <Fact(Skip:="https://github.com/dotnet/roslyn/issues/1212"), WorkItem("https://github.com/dotnet/roslyn/issues/1212")>
        Public Sub Queries_CapturedTransparentIdentifiers_JoinClause1()
            Dim src1 As String = "
Imports System
Imports System.Linq

Class C
    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1} 
                     Group Join b In {3} On Z(Function() a + 1) Equals Z(Function() b - 1) Into g = Group
                     Select Z(Function() g.First())
    End Sub
End Class
"
            Dim src2 As String = "
Imports System
Imports System.Linq

Class C
    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1} 
                     Group Join b In {3} On Z(Function() a + 1) Equals Z(Function() b - 1) Into g = Group
                     Select Z(Function() g.Last())
    End Sub
End Class"
            Dim edits = GetTopEdits(src1, src2)

            ' TODO (bug 1212) should report no error
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ComplexQueryExpression, "Group Join", "method"))
        End Sub

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/1312")>
        Public Sub Queries_CeaseCapturingTransparentIdentifiers1()
            Dim src1 As String = "
Imports System
Imports System.Linq

Class C

    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1}
                     From b In {2} 
                     Where Z(Function() a + b) > 0 
                     Select a
    End Sub
End Class
"
            Dim src2 As String = "
Imports System
Imports System.Linq

Class C

    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1}
                     From b In {2} 
                     Where Z(Function() a + 1) > 0 
                     Select a
    End Sub
End Class"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/1312")>
        Public Sub Queries_CapturingTransparentIdentifiers1()
            Dim src1 As String = "
Imports System
Imports System.Linq

Class C

    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1} 
                     From b In {2} 
                     Where Z(Function() a + 1) > 0 
                     Select a
    End Sub
End Class
"
            Dim src2 As String = "
Imports System
Imports System.Linq

Class C

    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1} 
                     From b In {2} 
                     Where Z(Function() a + b) > 0 
                     Select a
    End Sub
End Class"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Queries_AccessingCapturedTransparentIdentifier1()
            Dim src1 = "
Imports System
Imports System.Linq

Class C
    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1} 
                     Where Z(Function() a) > 0
                     Select 1
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq

Class C
    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1}
                     Where Z(Function() a) > 0 
                     Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Queries_AccessingCapturedTransparentIdentifier2()
            Dim src1 = "
Imports System
Imports System.Linq

Class C
    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1} 
                     From b In {1}
                     Where Z(Function() a) > 0
                     Select b
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq

Class C
    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1} 
                     From b In {1} 
                     Where Z(Function() a) > 0 
                     Select a + b
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Queries_AccessingCapturedTransparentIdentifier3()
            Dim src1 = "
Imports System
Imports System.Linq

Class C
    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1} 
                     Where Z(Function() a) > 0 
                     Select Z(Function() 1)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq

Class C
    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1} 
                     Where Z(Function() a) > 0 
                     Select Z(Function() a)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Queries_NotAccessingCapturedTransparentIdentifier1()
            Dim src1 = "
Imports System
Imports System.Linq

Class C
    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1} 
                     From b In {1}
                     Where Z(Function() a) > 0
                     Select a + b
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq

Class C
    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1} 
                     From b In {1} 
                     Where Z(Function() a) > 0 
                     Select b
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Queries_NotAccessingCapturedTransparentIdentifier2()
            Dim src1 = "
Imports System
Imports System.Linq

Class C
    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1} 
                     Where Z(Function() a) > 0 
                     Select Z(Function() a)
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq

Class C
    Function Z(f As Func(Of Integer)) As Integer
        Return 1
    End Function

    Sub F()
        Dim result = From a In {1} 
                     Where Z(Function() a) > 0 
                     Select Z(Function() 1)
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemantics(
                semanticEdits:={SemanticEdit(SemanticEditKind.Update, Function(c) c.GetMember("C.F"), preserveLocalVariables:=True)})
        End Sub

        <Fact>
        Public Sub Queries_Insert1()
            Dim src1 = "
Imports System
Imports System.Linq

Class C
    Sub F()
    End Sub
End Class
"
            Dim src2 = "
Imports System
Imports System.Linq

Class C
    Sub F()
        Dim result = From a In {1} Select a
    End Sub
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics(
                capabilities:=
                    EditAndContinueCapabilities.AddMethodToExistingType Or
                    EditAndContinueCapabilities.AddStaticFieldToExistingType Or
                    EditAndContinueCapabilities.NewTypeDefinition)
        End Sub
#End Region

#Region "Yield"
        <Fact>
        Public Sub Yield_Update1()
            Dim src1 = <text>
Yield 1
Yield 2
</text>.Value
            Dim src2 = <text>
Yield 3
Yield 4
</text>.Value
            Dim edits = GetMethodEdits(src1, src2, methodKind:=MethodKind.Iterator)

            edits.VerifyEdits(
                "Update [Yield 1]@50 -> [Yield 3]@50",
                "Update [Yield 2]@58 -> [Yield 4]@58")

        End Sub

        <Fact>
        Public Sub Yield_Update2()
            Dim src1 = <text>
Yield 1
Yield 2
</text>.Value
            Dim src2 = <text>
Yield 3
Yield 4
</text>.Value
            Dim edits = GetMethodEdits(src1, src2, methodKind:=MethodKind.Iterator)

            edits.VerifyEdits(
                "Update [Yield 1]@50 -> [Yield 3]@50",
                "Update [Yield 2]@58 -> [Yield 4]@58")
        End Sub

        <Fact>
        Public Sub Yield_Insert()
            Dim src1 = <text>
Yield 1
Yield 2
</text>.Value
            Dim src2 = <text>
Yield 1
Yield 2
Yield 3
</text>.Value
            Dim edits = GetMethodEdits(src1, src2, methodKind:=MethodKind.Iterator)

            edits.VerifyEdits(
                "Insert [Yield 3]@66")
        End Sub

        <Fact>
        Public Sub Yield_Delete()
            Dim src1 = <text>
Yield 1
Yield 2
Yield 3
</text>.Value
            Dim src2 = <text>
Yield 1
Yield 2
</text>.Value
            Dim edits = GetMethodEdits(src1, src2, methodKind:=MethodKind.Iterator)

            edits.VerifyEdits(
                "Delete [Yield 3]@66")
        End Sub

        <Fact>
        Public Sub MissingIteratorStateMachineAttribute()
            Dim src1 = "
Imports System.Collections.Generic

Class C
    Shared Iterator Function F() As IEnumerable(Of Integer)
        Yield 1
    End Function
End Class
"
            Dim src2 = "
Imports System.Collections.Generic

Class C
    Shared Iterator Function F() As IEnumerable(Of Integer)
        Yield 2
    End Function
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            VerifySemantics(
                edits,
                diagnostics:={Diagnostic(RudeEditKind.UpdatingStateMachineMethodMissingAttribute, "Shared Iterator Function F()", "System.Runtime.CompilerServices.IteratorStateMachineAttribute")},
                targetFrameworks:={TargetFramework.Mscorlib40AndSystemCore})
        End Sub

        <Fact>
        Public Sub MissingIteratorStateMachineAttribute2()
            Dim src1 = "
Imports System.Collections.Generic

Class C
    Shared Function F() As IEnumerable(Of Integer)
        Return Nothing
    End Function
End Class
"
            Dim src2 = "
Imports System.Collections.Generic

Class C
    Shared Iterator Function F() As IEnumerable(Of Integer)
        Yield 2
    End Function
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            VerifySemanticDiagnostics(
                editScript:=edits,
                targetFrameworks:={TargetFramework.Mscorlib40AndSystemCore},
                capabilities:=EditAndContinueCapabilities.NewTypeDefinition Or EditAndContinueCapabilities.AddExplicitInterfaceImplementation)
        End Sub

#End Region

#Region "Await"
        <Fact>
        Public Sub Await_Update1()
            Dim src1 = <text>
Await F(1)
Await F(2)
</text>.Value
            Dim src2 = <text>
Await F(3)
Await F(4)
</text>.Value
            Dim edits = GetMethodEdits(src1, src2, methodKind:=MethodKind.Async)

            edits.VerifyEdits(
                "Update [Await F(1)]@40 -> [Await F(3)]@40",
                "Update [Await F(2)]@51 -> [Await F(4)]@51")
        End Sub

        <Fact>
        Public Sub Await_Insert()
            Dim src1 = "
Await F(1)
Await F(3)"
            Dim src2 = "
Await F(1)
G(1, G(Await F(2)))
Await F(3)"
            Dim edits = GetMethodEdits(src1, src2, methodKind:=MethodKind.Async)

            edits.VerifyEdits(
                "Insert [G(1, G(Await F(2)))]@53",
                "Insert [Await F(2)]@60")
        End Sub

        <Fact>
        Public Sub Await_Delete()
            Dim src1 = "
Await F(1)
G(1, G(Await F(2)))
Await F(3)"
            Dim src2 = "
Await F(1)
Await F(3)"
            Dim edits = GetMethodEdits(src1, src2, methodKind:=MethodKind.Async)

            edits.VerifyEdits(
                "Delete [G(1, G(Await F(2)))]@53",
                "Delete [Await F(2)]@60")
        End Sub

        <Fact>
        Public Sub MissingAsyncStateMachineAttribute1()
            Dim src1 = "
Imports System.Threading.Tasks

Class C
    Shared Async Function F() As Task(Of Integer)
        Await New Task()
        Return 1
    End Function
End Class
"
            Dim src2 = "
Imports System.Threading.Tasks

Class C
    Shared Async Function F() As Task(Of Integer)
        Await New Task()
        Return 2
    End Function
End Class
"
            Dim edits = GetTopEdits(src1, src2)

            VerifySemantics(
                edits,
                diagnostics:={Diagnostic(RudeEditKind.UpdatingStateMachineMethodMissingAttribute, "Shared Async Function F()", "System.Runtime.CompilerServices.AsyncStateMachineAttribute")},
                targetFrameworks:={TargetFramework.MinimalAsync})
        End Sub

        <Fact>
        Public Sub MissingAsyncStateMachineAttribute2()
            Dim src1 = "
Imports System.Threading.Tasks

Class C
    Shared Function F() As Task(Of Integer)
        Return Nothing
    End Function
End Class
"
            Dim src2 = "
Imports System.Threading.Tasks

Class C
    Shared Async Function F() As Task(Of Integer)
        Await New Task()
        Return 2
    End Function
End Class
"
            Dim edits = GetTopEdits(src1, src2)
            VerifySemanticDiagnostics(
                edits,
                targetFrameworks:={TargetFramework.MinimalAsync},
                capabilities:=EditAndContinueCapabilities.NewTypeDefinition Or EditAndContinueCapabilities.AddExplicitInterfaceImplementation)
        End Sub

        <Theory>
        <InlineData("Await F(old)")>
        <InlineData("If Await F(old) Then : End If")>
        <InlineData("While Await F(old) : End While")>
        <InlineData("Do : Loop Until Await F(old)")>
        <InlineData("Do : Loop While Await F(old)")>
        <InlineData("Do Until Await F(old) : Loop")>
        <InlineData("Do While Await F(old) : Loop")>
        <InlineData("For i As Integer = 1 To Await F(old) : Next")>
        <InlineData("Using a = Await F(old) : End Using")>
        <InlineData("Dim a = Await F(old)")>
        <InlineData("Dim b = Await F(old), c = Await F(old)")>
        <InlineData("b = Await F(old)")>
        <InlineData("Select Case Await F(old) : Case 1 : Return Await F(old) : End Select")>
        <InlineData("Return Await F(old)")>
        Public Sub MethodUpdate_AsyncMethod2(oldStatement As String, Optional newStatement As String = Nothing)
            Dim src1 = "
Class C
    Async Function F(x As Integer) As Task(Of Integer)
        " & oldStatement & "
    End Function
End Class"

            newStatement = If(newStatement, oldStatement.Replace("old", "[new]"))

            Dim src2 = "
Class C
    Async Function F(x As Integer) As Task(Of Integer)
        " & newStatement & "
    End Function
End Class"

            Dim edits = GetTopEdits(src1, src2)
            edits.VerifySemanticDiagnostics(
                capabilities:=EditAndContinueCapabilities.AddInstanceFieldToExistingType)
        End Sub

        <Theory>
        <InlineData("F(old, Await F(old))")>
        <InlineData("F(1, Await F(old))")>
        <InlineData("F(Await F(old))")>
        <InlineData("Await F(Await F(old))")>
        <InlineData("If Await F(Await F(old)) Then : End If", {"Await F(Await F([new]))"})>
        <InlineData("Dim a = F(1, Await F(old)), b = F(1, Await G(old))", {"Dim a = F(1, Await F([new])), b = F(1, Await G([new]))", "Dim a = F(1, Await F([new])), b = F(1, Await G([new]))"})>
        <InlineData("For Each i In {Await F(old)} : Next", {"{Await F([new])}"})>
        <InlineData("x = F(1, Await F(old))")>
        <InlineData("x += Await F(old)")>
        Public Sub AwaitSpilling_Errors(oldStatement As String, Optional errorMessages As String() = Nothing)
            Dim src1 = "
Class C
    Async Function F(x As Integer) As Task(Of Integer)
        " & oldStatement & "
    End Function
End Class"
            Dim newStatement = oldStatement.Replace("old", "[new]")

            Dim src2 = "
Class C
    Async Function F(x As Integer) As Task(Of Integer)
        " & newStatement & "
    End Function
End Class"

            ' consider: these edits can be allowed if we get more sophisticated
            Dim edits = GetTopEdits(src1, src2)

            Dim expectedDiagnostics = From errorMessage In If(errorMessages, {newStatement})
                                      Select Diagnostic(RudeEditKind.AwaitStatementUpdate, errorMessage)

            edits.VerifySemanticDiagnostics(expectedDiagnostics.ToArray())
        End Sub
#End Region
    End Class
End Namespace
