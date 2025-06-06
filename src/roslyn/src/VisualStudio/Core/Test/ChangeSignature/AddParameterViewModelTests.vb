﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Threading
Imports System.Windows
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Collections
Imports Microsoft.CodeAnalysis.Editor.UnitTests.Extensions
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.VisualStudio.LanguageServices.Implementation.ChangeSignature
Imports Roslyn.Test.Utilities

Namespace Microsoft.VisualStudio.LanguageServices.UnitTests.ChangeSignature
    <UseExportProvider, Trait(Traits.Feature, Traits.Features.ChangeSignature)>
    Public Class AddParameterViewModelTests

        <WpfFact>
        Public Async Function AddParameter_SubmittingRequiresTypeAndNameAndCallsiteValue() As Task
            Dim markup = <Text><![CDATA[
class MyClass
{
    public void M($$) { }
}"]]></Text>

            Dim viewModelTestState = Await GetViewModelTestStateAsync(markup, LanguageNames.CSharp)
            Dim viewModel = viewModelTestState.ViewModel

            VerifyOpeningState(viewModel)

            viewModel.VerbatimTypeName = "int"
            Dim message As String = Nothing
            Assert.False(viewModel.CanSubmit(message))
            Assert.Equal(ServicesVSResources.A_type_and_name_must_be_provided, message)

            viewModel.VerbatimTypeName = ""
            viewModel.ParameterName = "x"
            Assert.False(viewModel.CanSubmit(message))
            Assert.Equal(ServicesVSResources.A_type_and_name_must_be_provided, message)

            viewModel.VerbatimTypeName = "int"
            Assert.False(viewModel.CanSubmit(message))
            Assert.Equal(ServicesVSResources.Enter_a_call_site_value_or_choose_a_different_value_injection_kind, message)

            viewModel.CallSiteValue = "7"
            Assert.True(viewModel.TrySubmit())
        End Function

        <WpfFact>
        Public Async Function AddParameter_TypeNameTextBoxInteractions() As Task
            Dim markup = <Text><![CDATA[
class MyClass<T>
{
    public void M($$) { }
}"]]></Text>

            Dim viewModelTestState = Await GetViewModelTestStateAsync(markup, LanguageNames.CSharp)
            Dim viewModel = viewModelTestState.ViewModel

            VerifyOpeningState(viewModel)

            Dim monitor = New PropertyChangedTestMonitor(viewModel)
            monitor.AddExpectation(Function() viewModel.TypeBindsDynamicStatus)
            monitor.AddExpectation(Function() viewModel.TypeIsEmptyImage)
            monitor.AddExpectation(Function() viewModel.TypeDoesNotParseOrInvalidTypeImage)
            monitor.AddExpectation(Function() viewModel.TypeDoesNotBindImage)
            monitor.AddExpectation(Function() viewModel.TypeBindsImage)

            viewModel.VerbatimTypeName = "M"

            monitor.VerifyExpectations()
            monitor.Detach()

            AssertTypeBindingIconAndTextIs(viewModel, NameOf(viewModel.TypeDoesNotBindImage), ServicesVSResources.Type_name_is_not_recognized)

            monitor = New PropertyChangedTestMonitor(viewModel)
            monitor.AddExpectation(Function() viewModel.TypeBindsDynamicStatus)
            monitor.AddExpectation(Function() viewModel.TypeIsEmptyImage)
            monitor.AddExpectation(Function() viewModel.TypeDoesNotParseOrInvalidTypeImage)
            monitor.AddExpectation(Function() viewModel.TypeDoesNotBindImage)
            monitor.AddExpectation(Function() viewModel.TypeBindsImage)

            viewModel.VerbatimTypeName = "MyClass"

            monitor.VerifyExpectations()
            monitor.Detach()

            AssertTypeBindingIconAndTextIs(viewModel, NameOf(viewModel.TypeDoesNotBindImage), ServicesVSResources.Type_name_is_not_recognized)

            monitor = New PropertyChangedTestMonitor(viewModel)
            monitor.AddExpectation(Function() viewModel.TypeBindsDynamicStatus)
            monitor.AddExpectation(Function() viewModel.TypeIsEmptyImage)
            monitor.AddExpectation(Function() viewModel.TypeDoesNotParseOrInvalidTypeImage)
            monitor.AddExpectation(Function() viewModel.TypeDoesNotBindImage)
            monitor.AddExpectation(Function() viewModel.TypeBindsImage)

            viewModel.VerbatimTypeName = "MyClass<i"

            monitor.VerifyExpectations()
            monitor.Detach()

            AssertTypeBindingIconAndTextIs(viewModel, NameOf(viewModel.TypeDoesNotParseOrInvalidTypeImage), ServicesVSResources.Type_name_has_a_syntax_error)

            monitor = New PropertyChangedTestMonitor(viewModel)
            monitor.AddExpectation(Function() viewModel.TypeBindsDynamicStatus)
            monitor.AddExpectation(Function() viewModel.TypeIsEmptyImage)
            monitor.AddExpectation(Function() viewModel.TypeDoesNotParseOrInvalidTypeImage)
            monitor.AddExpectation(Function() viewModel.TypeDoesNotBindImage)
            monitor.AddExpectation(Function() viewModel.TypeBindsImage)

            viewModel.VerbatimTypeName = "MyClass<int>"

            monitor.VerifyExpectations()
            monitor.Detach()

            AssertTypeBindingIconAndTextIs(viewModel, NameOf(viewModel.TypeBindsImage), ServicesVSResources.Type_name_is_recognized)

            monitor = New PropertyChangedTestMonitor(viewModel)
            monitor.AddExpectation(Function() viewModel.TypeBindsDynamicStatus)
            monitor.AddExpectation(Function() viewModel.TypeIsEmptyImage)
            monitor.AddExpectation(Function() viewModel.TypeDoesNotParseOrInvalidTypeImage)
            monitor.AddExpectation(Function() viewModel.TypeDoesNotBindImage)
            monitor.AddExpectation(Function() viewModel.TypeBindsImage)

            viewModel.VerbatimTypeName = ""

            monitor.VerifyExpectations()
            monitor.Detach()

            AssertTypeBindingIconAndTextIs(viewModel, NameOf(viewModel.TypeIsEmptyImage), ServicesVSResources.Please_enter_a_type_name)
        End Function

        <WpfTheory>
        <InlineData("int")>
        <InlineData("MyClass")>
        <InlineData("NS1.NS2.DifferentClass")>
        Public Async Function AddParameter_NoExistingParameters_TypeBinds(typeName As String) As Task
            Dim markup = <Text><![CDATA[
namespace NS1
{
    namespace NS2
    {
        class DifferentClass { }
    }
}

class MyClass
{
    public void M($$)
    {
        M();
    }
}"]]></Text>

            Dim viewModelTestState = Await GetViewModelTestStateAsync(markup, LanguageNames.CSharp)
            Dim viewModel = viewModelTestState.ViewModel

            VerifyOpeningState(viewModel)

            Dim monitor = New PropertyChangedTestMonitor(viewModel)
            monitor.AddExpectation(Function() viewModel.TypeBindsDynamicStatus)
            monitor.AddExpectation(Function() viewModel.TypeIsEmptyImage)
            monitor.AddExpectation(Function() viewModel.TypeDoesNotParseOrInvalidTypeImage)
            monitor.AddExpectation(Function() viewModel.TypeDoesNotBindImage)
            monitor.AddExpectation(Function() viewModel.TypeBindsImage)

            viewModel.VerbatimTypeName = typeName

            monitor.VerifyExpectations()
            monitor.Detach()

            AssertTypeBindingIconAndTextIs(viewModel, NameOf(viewModel.TypeBindsImage), ServicesVSResources.Type_name_is_recognized)

            viewModel.ParameterName = "x"
            viewModel.CallSiteValue = "0"

            Assert.True(viewModel.TrySubmit())
        End Function

        <WpfFact>
        Public Async Function AddParameter_CannotBeBothRequiredAndOmit() As Task
            Dim markup = <Text><![CDATA[
class MyClass<T>
{
    public void M($$) { }
}"]]></Text>

            Dim viewModelTestState = Await GetViewModelTestStateAsync(markup, LanguageNames.CSharp)
            Dim viewModel = viewModelTestState.ViewModel

            VerifyOpeningState(viewModel)

            Dim monitor = New PropertyChangedTestMonitor(viewModel)
            monitor.AddExpectation(Function() viewModel.IsOptional)
            monitor.AddExpectation(Function() viewModel.IsRequired)
            monitor.AddExpectation(Function() viewModel.IsCallsiteRegularValue)
            monitor.AddExpectation(Function() viewModel.IsCallsiteOmitted)

            viewModel.IsOptional = True
            viewModel.IsCallsiteOmitted = True
            viewModel.IsRequired = True

            monitor.VerifyExpectations()
            monitor.Detach()

            Assert.True(viewModel.IsCallsiteRegularValue)
            Assert.False(viewModel.IsCallsiteOmitted)
        End Function

        <WpfTheory>
        <InlineData("int")>
        <InlineData("MyClass")>
        <InlineData("NS1.NS2.DifferentClass")>
        Public Async Function AddParameter_ExistingParameters_TypeBinds(typeName As String) As Task
            Dim markup = <Text><![CDATA[
namespace NS1
{
    namespace NS2
    {
        class DifferentClass { }
    }
}

class MyClass
{
    public void M(int x$$)
    {
        M(3);
    }
}"]]></Text>

            Dim viewModelTestState = Await GetViewModelTestStateAsync(markup, LanguageNames.CSharp)
            Dim viewModel = viewModelTestState.ViewModel

            VerifyOpeningState(viewModel)

            Dim monitor = New PropertyChangedTestMonitor(viewModel)
            monitor.AddExpectation(Function() viewModel.TypeBindsDynamicStatus)
            monitor.AddExpectation(Function() viewModel.TypeIsEmptyImage)
            monitor.AddExpectation(Function() viewModel.TypeDoesNotParseOrInvalidTypeImage)
            monitor.AddExpectation(Function() viewModel.TypeDoesNotBindImage)
            monitor.AddExpectation(Function() viewModel.TypeBindsImage)

            viewModel.VerbatimTypeName = typeName

            monitor.VerifyExpectations()
            monitor.Detach()

            AssertTypeBindingIconAndTextIs(viewModel, NameOf(viewModel.TypeBindsImage), ServicesVSResources.Type_name_is_recognized)

            viewModel.ParameterName = "x"
            viewModel.CallSiteValue = "0"

            Assert.True(viewModel.TrySubmit())
        End Function

        Private Shared Sub AssertTypeBindingIconAndTextIs(viewModel As AddParameterDialogViewModel, currentIcon As String, expectedMessage As String)
            Assert.True(viewModel.TypeIsEmptyImage = If(NameOf(viewModel.TypeIsEmptyImage) = currentIcon, Visibility.Visible, Visibility.Collapsed))
            Assert.True(viewModel.TypeDoesNotParseOrInvalidTypeImage = If(NameOf(viewModel.TypeDoesNotParseOrInvalidTypeImage) = currentIcon, Visibility.Visible, Visibility.Collapsed))
            Assert.True(viewModel.TypeDoesNotBindImage = If(NameOf(viewModel.TypeDoesNotBindImage) = currentIcon, Visibility.Visible, Visibility.Collapsed))
            Assert.True(viewModel.TypeBindsImage = If(NameOf(viewModel.TypeBindsImage) = currentIcon, Visibility.Visible, Visibility.Collapsed))

            Assert.Equal(expectedMessage, viewModel.TypeBindsDynamicStatus)
        End Sub

        Private Shared Sub VerifyOpeningState(viewModel As AddParameterDialogViewModel)
            Assert.True(viewModel.TypeBindsDynamicStatus = ServicesVSResources.Please_enter_a_type_name)

            Assert.True(viewModel.TypeIsEmptyImage = Visibility.Visible)
            Assert.True(viewModel.TypeDoesNotParseOrInvalidTypeImage = Visibility.Collapsed)
            Assert.True(viewModel.TypeDoesNotBindImage = Visibility.Collapsed)
            Assert.True(viewModel.TypeBindsImage = Visibility.Collapsed)

            Assert.True(viewModel.IsRequired)
            Assert.False(viewModel.IsOptional)
            Assert.Equal(String.Empty, viewModel.DefaultValue)

            Assert.True(viewModel.IsCallsiteRegularValue)
            Assert.Equal(String.Empty, viewModel.CallSiteValue)
            Assert.False(viewModel.UseNamedArguments)
            Assert.False(viewModel.IsCallsiteTodo)
            Assert.False(viewModel.IsCallsiteOmitted)

            Dim message As String = Nothing
            Assert.False(viewModel.CanSubmit(message))
            Assert.Equal(ServicesVSResources.A_type_and_name_must_be_provided, message)
        End Sub

        Private Shared Async Function GetViewModelTestStateAsync(
            markup As XElement,
            languageName As String) As Task(Of AddParameterViewModelTestState)

            Dim workspaceXml =
            <Workspace>
                <Project Language=<%= languageName %> CommonReferences="true">
                    <Document><%= markup.NormalizedValue.Replace(vbCrLf, vbLf) %></Document>
                </Project>
            </Workspace>

            Using workspace = EditorTestWorkspace.Create(workspaceXml, composition:=VisualStudioTestCompositions.LanguageServices)
                Dim doc = workspace.Documents.Single()
                Dim workspaceDoc = workspace.CurrentSolution.GetDocument(doc.Id)
                If Not doc.CursorPosition.HasValue Then
                    Assert.True(False, "Missing caret location in document.")
                End If

                Dim document = Await SemanticDocument.CreateAsync(workspaceDoc, CancellationToken.None)
                Dim viewModel = New AddParameterDialogViewModel(document, doc.CursorPosition.Value)
                Return New AddParameterViewModelTestState(viewModel)
            End Using
        End Function

        <WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/44958")>
        Public Async Function AddParameter_SubmittingTypeWithModifiersIsInvalid() As Task
            Dim markup = <Text><![CDATA[
class MyClass
{
    public void M($$) { }
}"]]></Text>

            Dim viewModelTestState = Await GetViewModelTestStateAsync(markup, LanguageNames.CSharp)
            Dim viewModel = viewModelTestState.ViewModel

            VerifyOpeningState(viewModel)

            viewModel.ParameterName = "x"
            viewModel.CallSiteValue = "1"

            viewModel.TypeSymbol = Nothing
            Dim message As String = Nothing

            viewModel.VerbatimTypeName = "ref int"
            Assert.False(viewModel.CanSubmit(message))
            Assert.Equal(ServicesVSResources.Parameter_type_contains_invalid_characters, message)

            viewModel.VerbatimTypeName = "this int"
            Assert.False(viewModel.CanSubmit(message))
            Assert.Equal(ServicesVSResources.Parameter_type_contains_invalid_characters, message)

            viewModel.VerbatimTypeName = "this ref int"
            Assert.False(viewModel.CanSubmit(message))
            Assert.Equal(ServicesVSResources.Parameter_type_contains_invalid_characters, message)

            viewModel.VerbatimTypeName = "out int"
            Assert.False(viewModel.CanSubmit(message))
            Assert.Equal(ServicesVSResources.Parameter_type_contains_invalid_characters, message)

            viewModel.VerbatimTypeName = "params int[]"
            Assert.False(viewModel.CanSubmit(message))
            Assert.Equal(ServicesVSResources.Parameter_type_contains_invalid_characters, message)
        End Function

        <WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/44959")>
        Public Async Function AddParameter_CannotSubmitVoidParameterType() As Task
            Dim markup = <Text><![CDATA[
class MyClass
{
    public void M($$) { }
}"]]></Text>

            Dim viewModelTestState = Await GetViewModelTestStateAsync(markup, LanguageNames.CSharp)
            Dim viewModel = viewModelTestState.ViewModel

            VerifyOpeningState(viewModel)

            viewModel.ParameterName = "test"
            Dim message As String = Nothing

            viewModel.VerbatimTypeName = "void"
            Assert.False(viewModel.CanSubmit(message))
            Assert.Equal(ServicesVSResources.SystemVoid_is_not_a_valid_type_for_a_parameter, message)

            viewModel.VerbatimTypeName = "System.Void"
            Assert.False(viewModel.CanSubmit(message))
            Assert.Equal(ServicesVSResources.SystemVoid_is_not_a_valid_type_for_a_parameter, message)
        End Function
    End Class
End Namespace
