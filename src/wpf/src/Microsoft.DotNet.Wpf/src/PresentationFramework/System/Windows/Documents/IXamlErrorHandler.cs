// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Description: Interface Xaml error handler.
//

namespace System.Windows.Documents
{
    internal interface IXamlErrorHandler
    {
        void Error(string message, XamlToRtfError xamlToRtfError);

        void FatalError(string message, XamlToRtfError xamlToRtfError);

        void IgnorableWarning(string message, XamlToRtfError xamlToRtfError);
    }
}
