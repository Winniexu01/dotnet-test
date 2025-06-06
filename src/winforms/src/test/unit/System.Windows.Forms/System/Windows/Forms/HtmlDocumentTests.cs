﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32.System.Variant;
using Windows.Win32.Web.MsHtml;

namespace System.Windows.Forms.Tests;

[Collection("Sequential")] // workaround for WebBrowser control corrupting memory when run on multiple UI threads
public class HtmlDocumentTests
{
    [WinFormsFact]
    public async Task HtmlDocument_ActiveLinkColor_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><title>Title</title></head></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.Equal(Color.FromArgb(0xFF, 0x00, 0x00, 0xFF), document.ActiveLinkColor);
    }

    public static IEnumerable<object[]> ActiveLinkColor_GetCustomValueOnBody_TestData()
    {
        yield return new object[] { null, Color.FromArgb(0xFF, 0x00, 0x00, 0xFF) };
        yield return new object[] { "", Color.FromArgb(0xFF, 0x00, 0x00, 0xFF) };
        yield return new object[] { "NoSuchName", Color.FromArgb(0xFF, 0x00, 0xC0, 0x0E) };
        yield return new object[] { "Invalid", Color.FromArgb(0xFF, 0x00, 0xA0, 0xD0) };
        yield return new object[] { "Red", Color.FromArgb(0xFF, 0xFF, 0x00, 0x00) };
        yield return new object[] { 0x123456, Color.FromArgb(0xFF, 0x11, 0x30, 0x60) };
        yield return new object[] { 0x12345678, Color.FromArgb(0xFF, 0x30, 0x41, 0x89) };
        yield return new object[] { "#", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
        yield return new object[] { "#1", Color.FromArgb(0xFF, 0x1, 0x00, 0x00) };
        yield return new object[] { "#123456", Color.FromArgb(0xFF, 0x12, 0x34, 0x56) };
        yield return new object[] { "#12345678", Color.FromArgb(0xFF, 0x12, 0x45, 0x78) };
        yield return new object[] { "abc#123456", Color.FromArgb(0xFF, 0xAB, 0x12, 0x56) };
        yield return new object[] { "#G", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
    }

    [WinFormsTheory]
    [MemberData(nameof(ActiveLinkColor_GetCustomValueOnBody_TestData))]
    public async Task HtmlDocument_ActiveLinkColor_GetCustomValueOnBody_ReturnsExpected(object value, Color expected)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        string html = $"<html><body alink={value}></html>";
        HtmlDocument document = await GetDocument(control, html);
        Assert.Equal(expected, document.ActiveLinkColor);
    }

    public static IEnumerable<object[]> ActiveLinkColor_GetCustomValueSet_TestData()
    {
        yield return new object[] { null, Color.FromArgb(0xFF, 0x00, 0x00, 0xFF) };
        yield return new object[] { "", Color.FromArgb(0xFF, 0x00, 0x00, 0xFF) };
        yield return new object[] { "NoSuchName", Color.FromArgb(0xFF, 0x00, 0xC0, 0x0E) };
        yield return new object[] { "Invalid", Color.FromArgb(0xFF, 0x00, 0xA0, 0xD0) };
        yield return new object[] { "Red", Color.FromArgb(0xFF, 0xFF, 0x00, 0x00) };
        yield return new object[] { 0x123456, Color.FromArgb(0xFF, 0x12, 0x34, 0x56) };
        yield return new object[] { 0x12345678, Color.FromArgb(0xFF, 0x00, 0x00, 0xFF) };
        yield return new object[] { "#", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
        yield return new object[] { "#1", Color.FromArgb(0xFF, 0x1, 0x00, 0x00) };
        yield return new object[] { "#123456", Color.FromArgb(0xFF, 0x12, 0x34, 0x56) };
        yield return new object[] { "#12345678", Color.FromArgb(0xFF, 0x12, 0x45, 0x78) };
        yield return new object[] { "abc#123456", Color.FromArgb(0xFF, 0xAB, 0x12, 0x56) };
        yield return new object[] { "#G", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
    }

    [WinFormsTheory]
    [MemberData(nameof(ActiveLinkColor_GetCustomValueSet_TestData))]
    public async Task HtmlDocument_ActiveLinkColor_GetCustomValueSet_ReturnsExpected(object value, Color expected)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><title>Title</title></head></html>";
        HtmlDocument document = await GetDocument(control, Html);

        validate();
        unsafe void validate()
        {
            using var iHTMLDocument2 = ComHelpers.GetComScope<IHTMLDocument2>(document.DomDocument);

            using var variantValue = VARIANT.FromObject(value);
            Assert.True(iHTMLDocument2.Value->put_alinkColor(variantValue).Succeeded);
            Assert.Equal(expected, document.ActiveLinkColor);
        }
    }

    [WinFormsTheory]
    [MemberData(nameof(Color_Set_TestData))]
    public async Task HtmlDocument_ActiveLinkColor_Set_GetReturnsExpected(Color value, Color expected, string expectedNative)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        validate();
        unsafe void validate()
        {
            document.ActiveLinkColor = value;
            Assert.Equal(expected, document.ActiveLinkColor);
            using var iHTMLDocument2 = ComHelpers.GetComScope<IHTMLDocument2>(document.DomDocument);
            VARIANT color = default;
            Assert.True(iHTMLDocument2.Value->get_alinkColor(&color).Succeeded);
            Assert.Equal(expectedNative, (string)color.ToObject());

            // Set same.
            document.ActiveLinkColor = value;
            Assert.Equal(expected, document.ActiveLinkColor);
            VARIANT color2 = default;
            Assert.True(iHTMLDocument2.Value->get_alinkColor(&color2).Succeeded);
            Assert.Equal(expectedNative, (string)color2.ToObject());
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_All_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><title>NewDocument</title></head></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlElementCollection collection = document.All;
        Assert.NotSame(collection, document.All);
        Assert.Equal(4, collection.Count);
        Assert.Equal("HTML", collection[0].TagName);
        Assert.Equal("HEAD", collection[1].TagName);
        Assert.Equal("TITLE", collection[2].TagName);
        Assert.Equal("BODY", collection[3].TagName);
    }

    [WinFormsFact]
    public async Task HtmlDocument_All_GetEmpty_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlElementCollection collection = document.All;
        Assert.NotSame(collection, document.All);
        Assert.Equal(4, collection.Count);
        Assert.Equal("HTML", collection[0].TagName);
        Assert.Equal("HEAD", collection[1].TagName);
        Assert.Equal("TITLE", collection[2].TagName);
        Assert.Equal("BODY", collection[3].TagName);
    }

    [WinFormsFact]
    public async Task HtmlDocument_ActiveElement_GetNotActiveWithBody_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body>InnerText</body></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlElement element = document.ActiveElement;
        Assert.NotSame(element, document.ActiveElement);
        Assert.Equal("InnerText", element.InnerText);
        Assert.Equal("BODY", element.TagName);
    }

    [WinFormsFact]
    public async Task HtmlDocument_ActiveElement_GetNotActiveWithoutBody_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlElement element = document.ActiveElement;
        Assert.NotSame(element, document.ActiveElement);
        Assert.Null(element.InnerText);
        Assert.Equal("BODY", document.ActiveElement.TagName);
    }

    [WinFormsFact]
    public async Task HtmlDocument_ActiveElement_GetNoActiveElement_ReturnsNull()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><h1>Title</h1><p id=\"target\">InnerText</p></body></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlElement target = document.GetElementById("target");

        HtmlElement active = document.ActiveElement;
        blur();
        unsafe void blur()
        {
            using var iHtmlElement2 = ComHelpers.GetComScope<IHTMLElement2>(active.DomElement);
            iHtmlElement2.Value->blur();
        }

        HtmlElement element = document.ActiveElement;
        Assert.Null(document.ActiveElement);
    }

    [WinFormsFact]
    public async Task HtmlDocument_BackColor_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><title>Title</title></head></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.Equal(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), document.BackColor);
    }

    public static IEnumerable<object[]> BackColor_GetCustomValueOnBody_TestData()
    {
        yield return new object[] { null, Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) };
        yield return new object[] { "", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) };
        yield return new object[] { "NoSuchName", Color.FromArgb(0xFF, 0x00, 0xC0, 0x0E) };
        yield return new object[] { "Invalid", Color.FromArgb(0xFF, 0x00, 0xA0, 0xD0) };
        yield return new object[] { "Red", Color.FromArgb(0xFF, 0xFF, 0x00, 0x00) };
        yield return new object[] { 0x123456, Color.FromArgb(0xFF, 0x11, 0x30, 0x60) };
        yield return new object[] { 0x12345678, Color.FromArgb(0xFF, 0x30, 0x41, 0x89) };
        yield return new object[] { "#", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
        yield return new object[] { "#1", Color.FromArgb(0xFF, 0x1, 0x00, 0x00) };
        yield return new object[] { "#123456", Color.FromArgb(0xFF, 0x12, 0x34, 0x56) };
        yield return new object[] { "#12345678", Color.FromArgb(0xFF, 0x12, 0x45, 0x78) };
        yield return new object[] { "abc#123456", Color.FromArgb(0xFF, 0xAB, 0x12, 0x56) };
        yield return new object[] { "#G", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
    }

    [WinFormsTheory]
    [MemberData(nameof(BackColor_GetCustomValueOnBody_TestData))]
    public async Task HtmlDocument_BackColor_GetCustomValueOnBody_ReturnsExpected(object value, Color expected)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        string html = $"<html><body bgcolor={value}></body></html>";
        HtmlDocument document = await GetDocument(control, html);
        Assert.Equal(expected, document.BackColor);
    }

    public static IEnumerable<object[]> BackColor_GetCustomValueSet_TestData()
    {
        yield return new object[] { null, Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) };
        yield return new object[] { "", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) };
        yield return new object[] { "NoSuchName", Color.FromArgb(0xFF, 0x00, 0xC0, 0x0E) };
        yield return new object[] { "Invalid", Color.FromArgb(0xFF, 0x00, 0xA0, 0xD0) };
        yield return new object[] { "Red", Color.FromArgb(0xFF, 0xFF, 0x00, 0x00) };
        yield return new object[] { 0x123456, Color.FromArgb(0xFF, 0x12, 0x34, 0x56) };
        yield return new object[] { 0x12345678, Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) };
        yield return new object[] { "#", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
        yield return new object[] { "#1", Color.FromArgb(0xFF, 0x1, 0x00, 0x00) };
        yield return new object[] { "#123456", Color.FromArgb(0xFF, 0x12, 0x34, 0x56) };
        yield return new object[] { "#12345678", Color.FromArgb(0xFF, 0x12, 0x45, 0x78) };
        yield return new object[] { "abc#123456", Color.FromArgb(0xFF, 0xAB, 0x12, 0x56) };
        yield return new object[] { "#G", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
    }

    [WinFormsTheory]
    [MemberData(nameof(BackColor_GetCustomValueSet_TestData))]
    public async Task HtmlDocument_BackColor_GetCustomValueSet_ReturnsExpected(object value, Color expected)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><title>Title</title></head></html>";
        HtmlDocument document = await GetDocument(control, Html);
        validate();
        unsafe void validate()
        {
            using var iHTMLDocument2 = ComHelpers.GetComScope<IHTMLDocument2>(document.DomDocument);
            using var variantValue = VARIANT.FromObject(value);
            Assert.True(iHTMLDocument2.Value->put_bgColor(variantValue).Succeeded);
            Assert.Equal(expected, document.BackColor);
        }
    }

    public static IEnumerable<object[]> Color_Set_TestData()
    {
        yield return new object[] { Color.Empty, Color.FromArgb(0xFF, 0x00, 0x00, 0x00), "#000000" };
        yield return new object[] { Color.Red, Color.FromArgb(0xFF, 0xFF, 0x00, 0x00), "#ff0000" };
        yield return new object[] { Color.White, Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), "#ffffff" };
        yield return new object[] { Color.FromArgb(0x12, 0x34, 0x56, 0x78), Color.FromArgb(0xFF, 0x34, 0x56, 0x78), "#345678" };
    }

    [WinFormsTheory]
    [MemberData(nameof(Color_Set_TestData))]
    public async Task HtmlDocument_BackColor_Set_GetReturnsExpected(Color value, Color expected, string expectedNative)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        validate();
        unsafe void validate()
        {
            document.BackColor = value;
            Assert.Equal(expected, document.BackColor);
            using var iHTMLDocument2 = ComHelpers.GetComScope<IHTMLDocument2>(document.DomDocument);
            VARIANT color = default;
            Assert.True(iHTMLDocument2.Value->get_bgColor(&color).Succeeded);
            Assert.Equal(expectedNative, (string)color.ToObject());

            // Set same.
            document.BackColor = value;
            Assert.Equal(expected, document.BackColor);
            VARIANT color2 = default;
            Assert.True(iHTMLDocument2.Value->get_bgColor(&color2).Succeeded);
            Assert.Equal(expectedNative, (string)color2.ToObject());
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_Body_GetWithBody_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body>InnerText</body></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlElement element = document.Body;
        Assert.NotSame(element, document.Body);
        Assert.Equal("InnerText", element.InnerText);
        Assert.Equal("BODY", element.TagName);
    }

    [WinFormsFact]
    public async Task HtmlDocument_Body_GetWithoutBody_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlElement element = document.Body;
        Assert.NotSame(element, document.Body);
        Assert.Null(element.InnerText);
        Assert.Equal("BODY", element.TagName);
    }

    [WinFormsFact]
    public async Task HtmlDocument_Body_GetNoBody_ReturnsNull()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body>InnerText</body></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlElement element = document.Body;
        DomNodeRemoveChild();

        Assert.Null(document.Body);

        unsafe void DomNodeRemoveChild()
        {
            using var iHtmlDomNode = ComHelpers.GetComScope<IHTMLDOMNode>(element.Parent.DomElement);
            using var domElement = ComHelpers.GetComScope<IHTMLDOMNode>(element.DomElement);
            using ComScope<IHTMLDOMNode> node = new(null);
            Assert.True(iHtmlDomNode.Value->removeChild(domElement, node).Succeeded);
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_Cookie_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.Equal(document.Cookie, document.Cookie);
    }

    [WinFormsTheory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("cookie", "cookie")]
    public async Task HtmlDocument_Cookie_Set_GetReturnsExpected(string value, string expected)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);

        // Cleanup!
        // WebBrowser is notorious for not cleaning after itself - clean all cookies before we set new ones
        document.ExecCommand("ClearAuthenticationCache", false, null);
        validate();

        unsafe void validate()
        {
            document.Cookie = value;
            Assert.Equal(expected, document.Cookie);
            using var iHTMLDocument2 = ComHelpers.GetComScope<IHTMLDocument2>(document.DomDocument);
            using BSTR cookie = default;
            Assert.True(iHTMLDocument2.Value->get_cookie(&cookie).Succeeded);
            Assert.Equal(expected, cookie.ToString());

            // Set same.
            document.Cookie = value;
            Assert.Equal(expected, document.Cookie);
            using BSTR cookie2 = default;
            Assert.True(iHTMLDocument2.Value->get_cookie(&cookie2).Succeeded);
            Assert.Equal(expected, cookie2.ToString());
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_DefaultEncoding_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.NotEmpty(document.DefaultEncoding);
        Assert.DoesNotContain('\0', document.DefaultEncoding);
    }

    [WinFormsFact]
    public async Task HtmlDocument_DefaultEncoding_GetWithCustomValueOnMeta_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><meta charset=\"UTF-8\" /></head></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.NotEmpty(document.DefaultEncoding);
        Assert.DoesNotContain('\0', document.DefaultEncoding);
    }

    [WinFormsFact]
    public async Task HtmlDocument_DefaultEncoding_GetCustomValueSet_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        validate();
        unsafe void validate()
        {
            using var iHTMLDocument2 = ComHelpers.GetComScope<IHTMLDocument2>(document.DomDocument);
            using BSTR charSet = new("UTF-8");
            Assert.True(iHTMLDocument2.Value->put_defaultCharset(charSet).Succeeded);
            Assert.NotEmpty(document.DefaultEncoding);
            Assert.DoesNotContain('\0', document.DefaultEncoding);
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_Domain_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body>InnerText</body></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.Empty(document.Domain);
    }

    [WinFormsTheory]
    [StringWithNullData]
    public async Task HtmlDocument_Domain_Set_ThrowsCOMException(string value)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body>InnerText</body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        Assert.Throws<COMException>(() => document.Domain = value);
        Assert.Empty(document.Domain);
    }

    [WinFormsFact]
    public async Task HtmlDocument_DomDocument_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body>InnerText</body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        object domDocument = document.DomDocument;
        Assert.Same(domDocument, document.DomDocument);
        Assert.True(domDocument.GetType().IsCOMObject);
        Assert.False(domDocument is IHTMLDOMNode.Interface);
        Assert.True(domDocument is IHTMLDocument.Interface);
        Assert.True(domDocument is IHTMLDocument2.Interface);
        Assert.True(domDocument is IHTMLDocument3.Interface);
        Assert.True(domDocument is IHTMLDocument4.Interface);
    }

    [WinFormsFact]
    public async Task HtmlDocument_Encoding_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.NotEmpty(document.Encoding);
        Assert.DoesNotContain('\0', document.Encoding);
    }

    [WinFormsFact]
    public async Task HtmlDocument_Encoding_GetWithCustomValueOnMeta_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><meta charset=\"UTF-8\" /></head></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.Equal("utf-8", document.Encoding);
    }

    [WinFormsFact]
    public async Task HtmlDocument_Encoding_GetCustomValueSet_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);

        validate();
        unsafe void validate()
        {
            using var iHTMLDocument2 = ComHelpers.GetComScope<IHTMLDocument2>(document.DomDocument);
            using BSTR charSet = new("UTF-8");
            Assert.True(iHTMLDocument2.Value->put_charset(charSet).Succeeded);
            Assert.Equal("utf-8", document.Encoding);
        }
    }

    [WinFormsTheory]
    [InlineData("utf-8", "utf-8")]
    [InlineData("UTF-8", "utf-8")]
    public async Task HtmlDocument_Encoding_Set_GetReturnsExpected(string value, string expected)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        validate();

        unsafe void validate()
        {
            using var iHTMLDocument2 = ComHelpers.GetComScope<IHTMLDocument2>(document.DomDocument);
            document.Encoding = value;
            Assert.Equal(expected, document.Encoding);
            BSTR charset = default;
            Assert.True(iHTMLDocument2.Value->get_charset(&charset).Succeeded);
            Assert.Equal(expected, charset.ToStringAndFree());

            // Set same.
            document.Encoding = value;
            Assert.Equal(expected, document.Encoding);
            BSTR charset2 = default;
            Assert.True(iHTMLDocument2.Value->get_charset(&charset2).Succeeded);
            Assert.Equal(expected, charset2.ToStringAndFree());
        }
    }

    [WinFormsTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("reasonable")]
    public async Task HtmlDocument_Charset_SetInvalid_ThrowsArgumentException(string value)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.Throws<ArgumentException>(() => document.Encoding = value);
    }

    [WinFormsFact]
    public async Task HtmlDocument_Focused_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.False(document.Focused);
    }

    [ActiveIssue("https://github.com/dotnet/winforms/issues/4906")]
    [WinFormsFact(Skip = "Flaky tests, see: https://github.com/dotnet/winforms/issues/4906")]
    public async Task HtmlDocument_Focused_GetFocused_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        validate();
        unsafe void validate()
        {
            using var iHTMLDocument4 = ComHelpers.GetComScope<IHTMLDocument4>(document.DomDocument);
            iHTMLDocument4.Value->focus();
            Assert.False(document.Focused);

            // Have to do it again.
            iHTMLDocument4.Value->focus();
            Assert.True(document.Focused);
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_ForeColor_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><title>Title</title></head></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.Equal(Color.FromArgb(0xFF, 0x00, 0x00, 0x00), document.ForeColor);
    }

    public static IEnumerable<object[]> ForeColor_GetCustomValueOnBody_TestData()
    {
        yield return new object[] { null, Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
        yield return new object[] { "", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
        yield return new object[] { "NoSuchName", Color.FromArgb(0xFF, 0x00, 0xC0, 0x0E) };
        yield return new object[] { "Invalid", Color.FromArgb(0xFF, 0x00, 0xA0, 0xD0) };
        yield return new object[] { "Red", Color.FromArgb(0xFF, 0xFF, 0x00, 0x00) };
        yield return new object[] { 0x123456, Color.FromArgb(0xFF, 0x11, 0x30, 0x60) };
        yield return new object[] { 0x12345678, Color.FromArgb(0xFF, 0x30, 0x41, 0x89) };
        yield return new object[] { "#", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
        yield return new object[] { "#1", Color.FromArgb(0xFF, 0x1, 0x00, 0x00) };
        yield return new object[] { "#123456", Color.FromArgb(0xFF, 0x12, 0x34, 0x56) };
        yield return new object[] { "#12345678", Color.FromArgb(0xFF, 0x12, 0x45, 0x78) };
        yield return new object[] { "abc#123456", Color.FromArgb(0xFF, 0xAB, 0x12, 0x56) };
        yield return new object[] { "#G", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
    }

    [WinFormsTheory]
    [MemberData(nameof(ForeColor_GetCustomValueOnBody_TestData))]
    public async Task HtmlDocument_ForeColor_GetCustomValueOnBody_ReturnsExpected(object value, Color expected)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        string html = $"<html><body text={value}></html>";
        HtmlDocument document = await GetDocument(control, html);
        Assert.Equal(expected, document.ForeColor);
    }

    public static IEnumerable<object[]> ForeColor_GetCustomValueSet_TestData()
    {
        yield return new object[] { null, Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
        yield return new object[] { "", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
        yield return new object[] { "NoSuchName", Color.FromArgb(0xFF, 0x00, 0xC0, 0x0E) };
        yield return new object[] { "Invalid", Color.FromArgb(0xFF, 0x00, 0xA0, 0xD0) };
        yield return new object[] { "Red", Color.FromArgb(0xFF, 0xFF, 0x00, 0x00) };
        yield return new object[] { 0x123456, Color.FromArgb(0xFF, 0x12, 0x34, 0x56) };
        yield return new object[] { 0x12345678, Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
        yield return new object[] { "#", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
        yield return new object[] { "#1", Color.FromArgb(0xFF, 0x1, 0x00, 0x00) };
        yield return new object[] { "#123456", Color.FromArgb(0xFF, 0x12, 0x34, 0x56) };
        yield return new object[] { "#12345678", Color.FromArgb(0xFF, 0x12, 0x45, 0x78) };
        yield return new object[] { "abc#123456", Color.FromArgb(0xFF, 0xAB, 0x12, 0x56) };
        yield return new object[] { "#G", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
    }

    [WinFormsTheory]
    [MemberData(nameof(ForeColor_GetCustomValueSet_TestData))]
    public async Task HtmlDocument_ForeColor_GetCustomValueSet_ReturnsExpected(object value, Color expected)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><title>Title</title></head></html>";
        HtmlDocument document = await GetDocument(control, Html);
        validate();
        unsafe void validate()
        {
            using var iHTMLDocument2 = ComHelpers.GetComScope<IHTMLDocument2>(document.DomDocument);
            using var variantValue = VARIANT.FromObject(value);
            Assert.True(iHTMLDocument2.Value->put_fgColor(variantValue).Succeeded);
            Assert.Equal(expected, document.ForeColor);
        }
    }

    [WinFormsTheory]
    [MemberData(nameof(Color_Set_TestData))]
    public async Task HtmlDocument_ForeColor_Set_GetReturnsExpected(Color value, Color expected, string expectedNative)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        validate();

        unsafe void validate()
        {
            using var iHTMLDocument2 = ComHelpers.GetComScope<IHTMLDocument2>(document.DomDocument);
            document.ForeColor = value;
            Assert.Equal(expected, document.ForeColor);
            VARIANT color = default;
            Assert.True(iHTMLDocument2.Value->get_fgColor(&color).Succeeded);
            Assert.Equal(expectedNative, (string)color.ToObject());

            // Set same.
            document.ForeColor = value;
            Assert.Equal(expected, document.ForeColor);
            VARIANT color2 = default;
            Assert.True(iHTMLDocument2.Value->get_fgColor(&color2).Succeeded);
            Assert.Equal(expectedNative, (string)color2.ToObject());
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_Forms_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><img id=\"img1\" /><img id=\"img2\" /><a id=\"link1\">Href</a><a id=\"link2\">Href</a><form id=\"form1\"></form><form id=\"form2\"></form></body></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlElementCollection collection = document.Forms;
        Assert.NotSame(collection, document.Forms);
        Assert.Equal(2, collection.Count);
        Assert.Equal("FORM", collection[0].TagName);
        Assert.Equal("form1", collection[0].GetAttribute("id"));
        Assert.Equal("FORM", collection[1].TagName);
        Assert.Equal("form2", collection[1].GetAttribute("id"));
    }

    [WinFormsFact]
    public async Task HtmlDocument_Forms_GetEmpty_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlElementCollection collection = document.Forms;
        Assert.NotSame(collection, document.Forms);
        Assert.Empty(collection);
    }

    [WinFormsFact]
    public async Task HtmlDocument_Images_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><img id=\"img1\" /><img id=\"img2\" /><a id=\"link1\">Href</a><a id=\"link2\">Href</a><form id=\"form1\"></form><form id=\"form2\"></form></body></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlElementCollection collection = document.Images;
        Assert.NotSame(collection, document.Images);
        Assert.Equal(2, collection.Count);
        Assert.Equal("IMG", collection[0].TagName);
        Assert.Equal("img1", collection[0].GetAttribute("id"));
        Assert.Equal("IMG", collection[1].TagName);
        Assert.Equal("img2", collection[1].GetAttribute("id"));
    }

    [WinFormsFact]
    public async Task HtmlDocument_Images_GetEmpty_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlElementCollection collection = document.Images;
        Assert.NotSame(collection, document.Images);
        Assert.Empty(collection);
    }

    [WinFormsFact]
    public async Task HtmlDocument_LinkColor_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><title>Title</title></head></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.Equal(Color.FromArgb(0xFF, 0x00, 0x00, 0xFF), document.LinkColor);
    }

    public static IEnumerable<object[]> LinkColor_GetCustomValueOnBody_TestData()
    {
        yield return new object[] { null, Color.FromArgb(0xFF, 0x00, 0x00, 0xFF) };
        yield return new object[] { "", Color.FromArgb(0xFF, 0x00, 0x00, 0xFF) };
        yield return new object[] { "NoSuchName", Color.FromArgb(0xFF, 0x00, 0xC0, 0x0E) };
        yield return new object[] { "Invalid", Color.FromArgb(0xFF, 0x00, 0xA0, 0xD0) };
        yield return new object[] { "Red", Color.FromArgb(0xFF, 0xFF, 0x00, 0x00) };
        yield return new object[] { 0x123456, Color.FromArgb(0xFF, 0x11, 0x30, 0x60) };
        yield return new object[] { 0x12345678, Color.FromArgb(0xFF, 0x30, 0x41, 0x89) };
        yield return new object[] { "#", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
        yield return new object[] { "#1", Color.FromArgb(0xFF, 0x1, 0x00, 0x00) };
        yield return new object[] { "#123456", Color.FromArgb(0xFF, 0x12, 0x34, 0x56) };
        yield return new object[] { "#12345678", Color.FromArgb(0xFF, 0x12, 0x45, 0x78) };
        yield return new object[] { "abc#123456", Color.FromArgb(0xFF, 0xAB, 0x12, 0x56) };
        yield return new object[] { "#G", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
    }

    [WinFormsTheory]
    [MemberData(nameof(LinkColor_GetCustomValueOnBody_TestData))]
    public async Task HtmlDocument_LinkColor_GetCustomValueOnBody_ReturnsExpected(object value, Color expected)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        string html = $"<html><body link={value}></html>";
        HtmlDocument document = await GetDocument(control, html);
        Assert.Equal(expected, document.LinkColor);
    }

    public static IEnumerable<object[]> LinkColor_GetCustomValueSet_TestData()
    {
        yield return new object[] { null, Color.FromArgb(0xFF, 0x00, 0x00, 0xFF) };
        yield return new object[] { "", Color.FromArgb(0xFF, 0x00, 0x00, 0xFF) };
        yield return new object[] { "NoSuchName", Color.FromArgb(0xFF, 0x00, 0xC0, 0x0E) };
        yield return new object[] { "Invalid", Color.FromArgb(0xFF, 0x00, 0xA0, 0xD0) };
        yield return new object[] { "Red", Color.FromArgb(0xFF, 0xFF, 0x00, 0x00) };
        yield return new object[] { 0x123456, Color.FromArgb(0xFF, 0x12, 0x34, 0x56) };
        yield return new object[] { 0x12345678, Color.FromArgb(0xFF, 0x00, 0x00, 0xFF) };
        yield return new object[] { "#", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
        yield return new object[] { "#1", Color.FromArgb(0xFF, 0x1, 0x00, 0x00) };
        yield return new object[] { "#123456", Color.FromArgb(0xFF, 0x12, 0x34, 0x56) };
        yield return new object[] { "#12345678", Color.FromArgb(0xFF, 0x12, 0x45, 0x78) };
        yield return new object[] { "abc#123456", Color.FromArgb(0xFF, 0xAB, 0x12, 0x56) };
        yield return new object[] { "#G", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
    }

    [WinFormsTheory]
    [MemberData(nameof(LinkColor_GetCustomValueSet_TestData))]
    public async Task HtmlDocument_LinkColor_GetCustomValueSet_ReturnsExpected(object value, Color expected)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><title>Title</title></head></html>";
        HtmlDocument document = await GetDocument(control, Html);
        validate();
        unsafe void validate()
        {
            using var iHTMLDocument2 = ComHelpers.GetComScope<IHTMLDocument2>(document.DomDocument);
            using var variantValue = VARIANT.FromObject(value);
            Assert.True(iHTMLDocument2.Value->put_linkColor(variantValue).Succeeded);
            Assert.Equal(expected, document.LinkColor);
        }
    }

    [WinFormsTheory]
    [MemberData(nameof(Color_Set_TestData))]
    public async Task HtmlDocument_LinkColor_Set_GetReturnsExpected(Color value, Color expected, string expectedNative)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        validate();
        unsafe void validate()
        {
            document.LinkColor = value;
            Assert.Equal(expected, document.LinkColor);
            using var iHTMLDocument2 = ComHelpers.GetComScope<IHTMLDocument2>(document.DomDocument);
            VARIANT color = default;
            Assert.True(iHTMLDocument2.Value->get_linkColor(&color).Succeeded);
            Assert.Equal(expectedNative, (string)color.ToObject());

            // Set same.
            document.LinkColor = value;
            Assert.Equal(expected, document.LinkColor);
            VARIANT color2 = default;
            Assert.True(iHTMLDocument2.Value->get_linkColor(&color2).Succeeded);
            Assert.Equal(expectedNative, (string)color2.ToObject());
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_Links_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><img id=\"img1\" /><img id=\"img2\" /><a href=\"#1\" id=\"link1\">Href</a><a href=\"#1\" id=\"link2\">Href</a><form id=\"form1\"></form><form id=\"form2\"></form></body></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlElementCollection collection = document.Links;
        Assert.NotSame(collection, document.Links);
        Assert.Equal(2, collection.Count);
        Assert.Equal("A", collection[0].TagName);
        Assert.Equal("link1", collection[0].GetAttribute("id"));
        Assert.Equal("A", collection[1].TagName);
        Assert.Equal("link2", collection[1].GetAttribute("id"));
    }

    [WinFormsFact]
    public async Task HtmlDocument_Links_GetEmpty_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlElementCollection collection = document.Links;
        Assert.NotSame(collection, document.Links);
        Assert.Empty(collection);
    }

    [WinFormsFact]
    public async Task HtmlDocument_RightToLeft_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.False(document.RightToLeft);
    }

    public static IEnumerable<object[]> RightToLeft_CustomValueOnHtml_TestData()
    {
        yield return new object[] { "rtl", true };
        yield return new object[] { "RTL", true };
        yield return new object[] { "ltr", false };
        yield return new object[] { "abc", false };
        yield return new object[] { "", false };
        yield return new object[] { "123", false };
    }

    [WinFormsTheory]
    [MemberData(nameof(RightToLeft_CustomValueOnHtml_TestData))]
    public async Task HtmlDocument_RightToLeft_GetCustomValueOnHtml_ReturnsExpected(string rtl, bool expected)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        string html = $"<html dir={rtl}></html>";
        HtmlDocument document = await GetDocument(control, html);
        Assert.Equal(expected, document.RightToLeft);
    }

    public static IEnumerable<object[]> RightToLeft_CustomValueSet_TestData()
    {
        yield return new object[] { "rtl", true };
        yield return new object[] { "RTL", true };
        yield return new object[] { "ltr", false };
        yield return new object[] { "", false };
    }

    [WinFormsTheory]
    [MemberData(nameof(RightToLeft_CustomValueSet_TestData))]
    public async Task HtmlDocument_RightToLeft_GetCustomValueSet_ReturnsExpected(string rtl, bool expected)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        string html = $"<html></html>";
        HtmlDocument document = await GetDocument(control, html);

        validate();
        unsafe void validate()
        {
            using var iHTMLDocument3 = ComHelpers.GetComScope<IHTMLDocument3>(document.DomDocument);
            using BSTR bstrRtl = new(rtl);
            Assert.True(iHTMLDocument3.Value->put_dir(bstrRtl).Succeeded);
            Assert.Equal(expected, document.RightToLeft);
        }
    }

    [WinFormsTheory]
    [InlineData(true, "rtl", "ltr")]
    [InlineData(false, "ltr", "rtl")]
    public async Task HtmlDocument_RightToLeft_Set_GetReturnsExpected(bool value, string expectedNative1, string expectedNative2)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        validate();
        unsafe void validate()
        {
            document.RightToLeft = value;
            Assert.Equal(value, document.RightToLeft);
            using var iHTMLDocument3 = ComHelpers.GetComScope<IHTMLDocument3>(document.DomDocument);
            using BSTR dir = default;
            Assert.True(iHTMLDocument3.Value->get_dir(&dir).Succeeded);
            Assert.Equal(expectedNative1, dir.ToString());

            // Set same.
            document.RightToLeft = value;
            Assert.Equal(value, document.RightToLeft);
            using BSTR dir2 = default;
            Assert.True(iHTMLDocument3.Value->get_dir(&dir2).Succeeded);
            Assert.Equal(expectedNative1, dir2.ToString());

            // Set different.
            document.RightToLeft = !value;
            Assert.Equal(!value, document.RightToLeft);
            using BSTR dir3 = default;
            Assert.True(iHTMLDocument3.Value->get_dir(&dir3).Succeeded);
            Assert.Equal(expectedNative2, dir3.ToString());
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_Title_GetWithTitle_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><title>Title</title></head></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.Equal("Title", document.Title);
    }

    [WinFormsFact]
    public async Task HtmlDocument_Title_GetNoTitle_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.Empty(document.Title);
    }

    [WinFormsTheory]
    [NormalizedStringData]
    public async Task HtmlDocument_Title_GetCustomValueSet_ReturnsExpected(string title, string expected)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        validate();
        unsafe void validate()
        {
            using var iHTMLDocument2 = ComHelpers.GetComScope<IHTMLDocument2>(document.DomDocument);
            using BSTR bstrTitle = new(title);
            Assert.True(iHTMLDocument2.Value->put_title(bstrTitle).Succeeded);
            Assert.Equal(expected, document.Title);
        }
    }

    [WinFormsTheory]
    [NormalizedStringData]
    public async Task HtmlDocument_Title_Set_GetReturnsExpected(string value, string expected)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        validate();
        unsafe void validate()
        {
            document.Title = value;
            Assert.Equal(expected, document.Title);
            using var iHTMLDocument2 = ComHelpers.GetComScope<IHTMLDocument2>(document.DomDocument);
            using BSTR title = default;
            Assert.True(iHTMLDocument2.Value->get_title(&title).Succeeded);
            Assert.Equal(expected, title.ToString());

            // Set same.
            document.Title = value;
            Assert.Equal(expected, document.Title);
            using BSTR title2 = default;
            Assert.True(iHTMLDocument2.Value->get_title(&title2).Succeeded);
            Assert.Equal(expected, title2.ToString());
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_Url_GetDocument_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        TaskCompletionSource<bool> source = new();
        control.DocumentCompleted += (sender, e) => source.SetResult(true);

        using var file = CreateTempFile(Html);
        await Task.Run(() => control.Navigate(file.Path));
        Assert.True(await source.Task);

        HtmlDocument document = control.Document;
        Assert.Equal(new Uri(file.Path), document.Url);
    }

    [WinFormsFact]
    public async Task HtmlDocument_VisitedLinkColor_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><title>Title</title></head></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.Equal(Color.FromArgb(0xFF, 0x80, 0x00, 0x80), document.VisitedLinkColor);
    }

    public static IEnumerable<object[]> VisitedLinkColor_GetCustomValueOnBody_TestData()
    {
        yield return new object[] { null, Color.FromArgb(0xFF, 0x80, 0x00, 0x80) };
        yield return new object[] { "", Color.FromArgb(0xFF, 0x80, 0x00, 0x80) };
        yield return new object[] { "NoSuchName", Color.FromArgb(0xFF, 0x00, 0xC0, 0x0E) };
        yield return new object[] { "Invalid", Color.FromArgb(0xFF, 0x00, 0xA0, 0xD0) };
        yield return new object[] { "Red", Color.FromArgb(0xFF, 0xFF, 0x00, 0x00) };
        yield return new object[] { 0x123456, Color.FromArgb(0xFF, 0x11, 0x30, 0x60) };
        yield return new object[] { 0x12345678, Color.FromArgb(0xFF, 0x30, 0x41, 0x89) };
        yield return new object[] { "#", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
        yield return new object[] { "#1", Color.FromArgb(0xFF, 0x1, 0x00, 0x00) };
        yield return new object[] { "#123456", Color.FromArgb(0xFF, 0x12, 0x34, 0x56) };
        yield return new object[] { "#12345678", Color.FromArgb(0xFF, 0x12, 0x45, 0x78) };
        yield return new object[] { "abc#123456", Color.FromArgb(0xFF, 0xAB, 0x12, 0x56) };
        yield return new object[] { "#G", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
    }

    [WinFormsTheory]
    [MemberData(nameof(VisitedLinkColor_GetCustomValueOnBody_TestData))]
    public async Task HtmlDocument_VisitedLinkColor_GetCustomValueOnBody_ReturnsExpected(object value, Color expected)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        string html = $"<html><body vlink={value}></html>";
        HtmlDocument document = await GetDocument(control, html);
        Assert.Equal(expected, document.VisitedLinkColor);
    }

    public static IEnumerable<object[]> VisitedLinkColor_GetCustomValueSet_TestData()
    {
        yield return new object[] { null, Color.FromArgb(0xFF, 0x80, 0x00, 0x80) };
        yield return new object[] { "", Color.FromArgb(0xFF, 0x80, 0x00, 0x80) };
        yield return new object[] { "NoSuchName", Color.FromArgb(0xFF, 0x00, 0xC0, 0x0E) };
        yield return new object[] { "Invalid", Color.FromArgb(0xFF, 0x00, 0xA0, 0xD0) };
        yield return new object[] { "Red", Color.FromArgb(0xFF, 0xFF, 0x00, 0x00) };
        yield return new object[] { 0x123456, Color.FromArgb(0xFF, 0x12, 0x34, 0x56) };
        yield return new object[] { 0x12345678, Color.FromArgb(0xFF, 0x80, 0x00, 0x80) };
        yield return new object[] { "#", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
        yield return new object[] { "#1", Color.FromArgb(0xFF, 0x1, 0x00, 0x00) };
        yield return new object[] { "#123456", Color.FromArgb(0xFF, 0x12, 0x34, 0x56) };
        yield return new object[] { "#12345678", Color.FromArgb(0xFF, 0x12, 0x45, 0x78) };
        yield return new object[] { "abc#123456", Color.FromArgb(0xFF, 0xAB, 0x12, 0x56) };
        yield return new object[] { "#G", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) };
    }

    [WinFormsTheory]
    [MemberData(nameof(VisitedLinkColor_GetCustomValueSet_TestData))]
    public async Task HtmlDocument_VisitedLinkColor_GetCustomValueSet_ReturnsExpected(object value, Color expected)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><title>Title</title></head></html>";
        HtmlDocument document = await GetDocument(control, Html);
        validate();
        unsafe void validate()
        {
            using var iHTMLDocument2 = ComHelpers.GetComScope<IHTMLDocument2>(document.DomDocument);
            using var variantValue = VARIANT.FromObject(value);
            Assert.True(iHTMLDocument2.Value->put_vlinkColor(variantValue).Succeeded);
            Assert.Equal(expected, document.VisitedLinkColor);
        }
    }

    [WinFormsTheory]
    [MemberData(nameof(Color_Set_TestData))]
    public async Task HtmlDocument_VisitedLinkColor_Set_GetReturnsExpected(Color value, Color expected, string expectedNative)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        validate();
        unsafe void validate()
        {
            document.VisitedLinkColor = value;
            Assert.Equal(expected, document.VisitedLinkColor);
            using var iHTMLDocument2 = ComHelpers.GetComScope<IHTMLDocument2>(document.DomDocument);
            VARIANT color = default;
            Assert.True(iHTMLDocument2.Value->get_vlinkColor(&color).Succeeded);
            Assert.Equal(expectedNative, (string)color.ToObject());

            // Set same.
            document.VisitedLinkColor = value;
            Assert.Equal(expected, document.VisitedLinkColor);
            VARIANT color2 = default;
            Assert.True(iHTMLDocument2.Value->get_vlinkColor(&color2).Succeeded);
            Assert.Equal(expectedNative, (string)color2.ToObject());
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_Window_Get_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body>InnerText</body></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlWindow window = document.Window;
        Assert.NotSame(window, document.Window);
        Assert.NotSame(document, window.Document);
        Assert.Equal("InnerText", window.Document.Body.InnerText);
    }

    [WinFormsTheory]
    [InlineData("eventName")]
    [InlineData("onclick")]
    public async Task HtmlDocument_AttachEventHandler_AttachDetach_Success(string eventName)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        int callCount = 0;
        void handler(object sender, EventArgs e) => callCount++;
        document.AttachEventHandler(eventName, handler);
        Assert.Equal(0, callCount);

        // Attach again.
        document.AttachEventHandler(eventName, handler);
        Assert.Equal(0, callCount);

        document.DetachEventHandler(eventName, handler);
        Assert.Equal(0, callCount);
    }

    [WinFormsTheory]
    [InlineData("onclick")]
    [InlineData("oncontextmenu")]
    [InlineData("onfocusin")]
    [InlineData("onfocusout")]
    [InlineData("onmousemove")]
    [InlineData("onmousedown")]
    [InlineData("onmouseout")]
    [InlineData("onmouseover")]
    [InlineData("onmouseup")]
    [InlineData("onstop")]
    [InlineData("onhelp")]
    [InlineData("ondblclick")]
    [InlineData("onkeydown")]
    [InlineData("onkeyup")]
    [InlineData("onkeypress")]
    [InlineData("onreadystatechange")]
    [InlineData("onbeforeupdate")]
    [InlineData("onafterupdate")]
    [InlineData("onrowexit")]
    [InlineData("onrowenter")]
    [InlineData("ondragstart")]
    [InlineData("onselectstart")]
    [InlineData("onerrorupdate")]
    [InlineData("onrowsdelete")]
    [InlineData("onrowsinserted")]
    [InlineData("oncellchange")]
    [InlineData("onpropertychange")]
    [InlineData("ondatasetchanged")]
    [InlineData("ondataavailable")]
    [InlineData("ondatasetcomplete")]
    [InlineData("onbeforeeditfocus")]
    [InlineData("onselectionchange")]
    [InlineData("oncontrolselect")]
    [InlineData("onmousewheel")]
    [InlineData("onactivate")]
    [InlineData("ondeactivate")]
    [InlineData("onbeforeactivate")]
    [InlineData("onbeforedeactivate")]
    public async Task HtmlDocument_AttachEventHandler_InvokeClick_Success(string eventName)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><p>InnerText</p></body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        int callCount = 0;
        void handler(object sender, EventArgs e)
        {
            Assert.NotSame(document, sender);
            Assert.Same(EventArgs.Empty, e);
            callCount++;
        }

        validate();
        unsafe void validate()
        {
            document.AttachEventHandler(eventName, handler);
            Assert.Equal(0, callCount);

            using var iHTMLDocument4 = ComHelpers.GetComScope<IHTMLDocument4>(document.DomDocument);
            using BSTR name = new(eventName);
            VARIANT eventObj = default;
            VARIANT_BOOL cancelled = default;
            Assert.True(iHTMLDocument4.Value->fireEvent(name, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);

            document.DetachEventHandler(eventName, handler);
            Assert.Equal(1, callCount);
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_AttachEventHandler_EmptyEventName_ThrowsCOMException()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        int callCount = 0;
        void handler(object sender, EventArgs e) => callCount++;
        COMException ex = Assert.Throws<COMException>(() => document.AttachEventHandler(string.Empty, handler));
        Assert.Equal(HRESULT.DISP_E_UNKNOWNNAME, (HRESULT)ex.HResult);
        Assert.Equal(0, callCount);
    }

    [WinFormsFact]
    public async Task HtmlDocument_AttachEventHandler_NullEventName_ThrowsArgumentException()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        int callCount = 0;
        void handler(object sender, EventArgs e) => callCount++;
        document.AttachEventHandler(null, handler);
        Assert.Equal(0, callCount);
    }

    [WinFormsTheory]
    [InlineData("", "")]
    [InlineData("TagName", "TagName")]
    [InlineData("h1", "H1")]
    public async Task HtmlDocument_CreateElement_Invoke_ReturnsExpected(string tagName, string expectedTagName)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body>InnerText</body></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlElement element = document.CreateElement(tagName);
        Assert.Equal(expectedTagName, element.TagName);
        Assert.NotSame(document, element.Document);
        Assert.Null(element.Document.Body);
    }

    [WinFormsFact]
    public async Task HtmlDocument_CreateElement_NullElementTag_ThrowsArgumentException()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.Throws<ArgumentException>(() => document.CreateElement(null));
    }

    [WinFormsTheory]
    [InlineData("eventName")]
    [InlineData("onclick")]
    public async Task HtmlDocument_DetachEventHandler_AttachDetach_Success(string eventName)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        int callCount = 0;
        void handler(object sender, EventArgs e) => callCount++;

        document.DetachEventHandler(eventName, handler);
        Assert.Equal(0, callCount);

        document.DetachEventHandler(eventName, handler);
        Assert.Equal(0, callCount);

        document.DetachEventHandler(eventName, handler);
        Assert.Equal(0, callCount);

        // Detach again.
        document.DetachEventHandler(eventName, handler);
        Assert.Equal(0, callCount);
    }

    [WinFormsFact]
    public async Task HtmlDocument_DetachEventHandler_EmptyEventName_ThrowsCOMException()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        int callCount = 0;
        void handler(object sender, EventArgs e) => callCount++;
        document.DetachEventHandler(string.Empty, handler);
        Assert.Equal(0, callCount);
    }

    [WinFormsFact]
    public async Task HtmlDocument_DetachEventHandler_NullEventName_ThrowsArgumentException()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        int callCount = 0;
        void handler(object sender, EventArgs e) => callCount++;
        document.DetachEventHandler(null, handler);
        Assert.Equal(0, callCount);
    }

    [WinFormsFact]
    public async Task HtmlDocument_Equals_Invoke_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><div id=\"id1\"></div><div id=\"id2\"></div></body></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlDocument newDocument = document.OpenNew(false);

        Assert.True(document.Equals(document));
        Assert.False(document.Equals(newDocument));
        Assert.Throws<InvalidCastException>(() => document.Equals(new object()));
        Assert.False(document.Equals(null));
    }

    [WinFormsTheory]
    [InlineData("copy", true, null)]
    [InlineData("copy", true, "abc")]
    [InlineData("copy", false, null)]
    [InlineData("copy", false, "def")]
    public async Task HtmlDocument_ExecCommand_Invoke_Success(string command, bool showUI, object value)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><title>NewDocument</title></head></html>";
        HtmlDocument document = await GetDocument(control, Html);
        document.ExecCommand(command, showUI, value);
    }

    [WinFormsTheory]
    [InlineData(null, true, null)]
    [InlineData("NoSuchCommand", true, null)]
    [InlineData(null, false, null)]
    [InlineData("NoSuchCommand", false, null)]
    public async Task HtmlDocument_ExecCommand_InvalidCommand_ThrowsCOMException(string command, bool showUI, object value)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><title>NewDocument</title></head></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.Throws<COMException>(() => document.ExecCommand(command, showUI, value));
    }

    [ActiveIssue("https://github.com/dotnet/winforms/issues/4906")]
    [WinFormsFact(Skip = "Flaky tests, see: https://github.com/dotnet/winforms/issues/4906")]
    public async Task HtmlDocument_Focus_Invoke_Success()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        document.Focus();
        Assert.True(document.Focused);

        // Call again.
        document.Focus();
        Assert.True(document.Focused);
    }

    [WinFormsFact]
    public async Task HtmlDocument_GetElementById_Invoke_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><p id=\"para1\">InnerText1</p><p id=\"para2\">InnerText2</p><p>InnerText3</p></body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        Assert.Equal("InnerText1", document.GetElementById("para1").InnerText);
        Assert.Equal("InnerText1", document.GetElementById("PARA1").InnerText);
        Assert.Null(document.GetElementById("NoSuchId"));
        Assert.Null(document.GetElementById(string.Empty));
    }

    [WinFormsFact]
    public async Task HtmlDocument_GetElementById_NullId_ThrowsArgumentException()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.Throws<ArgumentException>(() => document.GetElementById(null));
    }

    [WinFormsFact]
    public async Task HtmlDocument_GetElementFromPoint_Invoke_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><p id=\"para1\">InnerText1</p><p id=\"para2\">InnerText2</p><p>InnerText3</p></body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        Assert.Equal("BODY", document.GetElementFromPoint(Point.Empty).TagName);
        Assert.Equal("BODY", document.GetElementFromPoint(new Point(int.MinValue, int.MinValue)).TagName);
        Assert.Equal("BODY", document.GetElementFromPoint(new Point(int.MaxValue, int.MaxValue)).TagName);
    }

    [WinFormsFact]
    public async Task HtmlDocument_GetElementsByTagName_Invoke_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><img id=\"img1\" /><img id=\"img2\" /><a id=\"link1\">Href</a><a id=\"link2\">Href</a><form id=\"form1\"></form><form id=\"form2\"></form></body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        HtmlElementCollection collection = document.GetElementsByTagName("form");
        Assert.NotSame(collection, document.GetElementsByTagName("form"));
        Assert.Equal(2, collection.Count);
        Assert.Equal("FORM", collection[0].TagName);
        Assert.Equal("form1", collection[0].GetAttribute("id"));
        Assert.Equal("FORM", collection[1].TagName);
        Assert.Equal("form2", collection[1].GetAttribute("id"));

        Assert.Empty(document.GetElementsByTagName("NoSuchTagName"));
        Assert.Equal("HTML", ((HtmlElement)Assert.Single(document.GetElementsByTagName("html"))).TagName);
        Assert.Empty(document.GetElementsByTagName(""));
    }

    [WinFormsFact]
    public async Task HtmlDocument_GetElementsByTagName_IndexIntoByString_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><img id=\"img1\" /><img id=\"img2\" /><a id=\"link1\">Href</a><a id=\"link2\">Href</a><form id=\"form1\"></form><form id=\"form2\"></form></body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        HtmlElementCollection collection = document.GetElementsByTagName("form");
        Assert.NotSame(collection, document.GetElementsByTagName("form"));
        Assert.Equal(2, collection.Count);
        Assert.NotNull(collection["form1"]);
        Assert.NotNull(collection["form2"]);
        Assert.Null(collection["form3"]);
    }

    [WinFormsFact]
    public async Task HtmlDocument_GetElementsByTagName_NullTagName_ThrowsArgumentException()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);
        Assert.Throws<ArgumentException>(() => document.GetElementsByTagName(null));
    }

    [WinFormsFact]
    public async Task HtmlDocument_GetHashCode_Invoke_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><div id=\"id1\"></div><div id=\"id2\"></div></body></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlDocument newDocument = document.OpenNew(false);

        Assert.NotEqual(0, document.GetHashCode());
        Assert.Equal(document.GetHashCode(), document.GetHashCode());
        Assert.NotEqual(document.GetHashCode(), newDocument.GetHashCode());
    }

    [WinFormsFact]
    public async Task HtmlDocument_InvokeScript_ScriptExists_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = @"
<html>
    <head>
        <script>
            function divide(p1, p2)
            {
                if (!p1)
                {
                    return ""NoParameter1"";
                }
                if (!p2)
                {
                    return ""NoParameter2"";
                }

                return p1 / p2;
            }
        </script>
    </head>
</html>";
        HtmlDocument document = await GetDocument(control, Html);

        Assert.Equal("NoParameter1", document.InvokeScript("divide"));
        Assert.Equal("NoParameter1", document.InvokeScript("divide", null));
        Assert.Equal("NoParameter1", document.InvokeScript("divide", Array.Empty<object>()));
        Assert.Equal("NoParameter2", document.InvokeScript("divide", [2]));
        Assert.Equal(6, document.InvokeScript("divide", [12, 2]));
        Assert.Equal(6, document.InvokeScript("divide", [12, 2]));
    }

    [WinFormsFact]
    public async Task HtmlDocument_InvokeScript_NoSuchScript_ReturnsNull()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><head><title>NewDocument</title></head></html>";
        HtmlDocument document = await GetDocument(control, Html);

        Assert.Null(document.InvokeScript("NoSuchScript"));
        Assert.Null(document.InvokeScript("NoSuchScript", null));
        Assert.Null(document.InvokeScript("NoSuchScript", Array.Empty<object>()));
        Assert.Null(document.InvokeScript("NoSuchScript", [1]));
    }

    public static IEnumerable<object[]> Write_TestData()
    {
        yield return new object[] { null, "undefined" };
        yield return new object[] { "", null };
        yield return new object[] { "InnerText", "InnerText" };
        yield return new object[] { "<p>Hi</p>", "<P>Hi</P>" };
    }

    [WinFormsTheory]
    [MemberData(nameof(Write_TestData))]
    public async Task HtmlDocument_Write_InvokeEmpty_Success(string text, string expectedInnerHtml)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html></html>";
        HtmlDocument document = await GetDocument(control, Html);

        document.Write(text);
        Assert.Equal(expectedInnerHtml, document.Body?.InnerHtml);
    }

    [WinFormsTheory]
    [MemberData(nameof(Write_TestData))]
    public async Task HtmlDocument_Write_InvokeNotEmpty_Success(string text, string expectedInnerHtml)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><h1>OldH1</h1></body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        document.Write(text);
        Assert.Equal(expectedInnerHtml, document.Body?.InnerHtml);
    }

    [WinFormsTheory]
    [BoolData]
    public async Task HtmlDocument_OpenNew_Invoke_Success(bool replaceInHistory)
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><h1>InnerText</h1></body></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlDocument newDocument = document.OpenNew(replaceInHistory);
        Assert.Empty(newDocument.All);
        Assert.Null(newDocument.Body);
        Assert.Equal("about:blank", newDocument.Url.OriginalString);
    }

#pragma warning disable CS1718, CSIsNull001, CSIsNull002 // Disable "Comparison made to same variable" warning.
    [WinFormsFact]
    public async Task HtmlDocument_OperatorEquals_Invoke_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><div id=\"id1\"></div><div id=\"id2\"></div></body></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlDocument newDocument = document.OpenNew(false);

        Assert.True(document == document);
        Assert.False(document == newDocument);
        Assert.NotNull(document);
        Assert.NotNull(document);
        Assert.Null((HtmlDocument)null);
    }

    [WinFormsFact]
    public async Task HtmlDocument_OperatorNotEquals_Invoke_ReturnsExpected()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><div id=\"id1\"></div><div id=\"id2\"></div></body></html>";
        HtmlDocument document = await GetDocument(control, Html);
        HtmlDocument newDocument = document.OpenNew(false);

        Assert.False(document != document);
        Assert.True(document != newDocument);
        Assert.NotNull(document);
        Assert.NotNull(document);
        Assert.Null((HtmlDocument)null);
    }
#pragma warning restore CS1718, CSIsNull001, CSIsNull002

    [WinFormsFact]
    public async Task HtmlDocument_Click_InvokeEvent_Success()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><p>InnerText</p></body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        int callCount = 0;
        void handler(object sender, EventArgs e)
        {
            Assert.Same(document, sender);
            Assert.IsType<HtmlElementEventArgs>(e);
            callCount++;
        }

        validate();
        unsafe void validate()
        {
            using var iHTMLDocument4 = ComHelpers.GetComScope<IHTMLDocument4>(document.DomDocument);
            document.Click += handler;
            using BSTR onClick = new("onclick");
            VARIANT eventObj = default;
            VARIANT_BOOL cancelled = default;
            Assert.True(iHTMLDocument4.Value->fireEvent(onClick, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);

            // Remove handler.
            document.Click -= handler;
            Assert.True(iHTMLDocument4.Value->fireEvent(onClick, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_ContextMenuShowing_InvokeEvent_Success()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><p>InnerText</p></body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        int callCount = 0;
        void handler(object sender, EventArgs e)
        {
            Assert.Same(document, sender);
            Assert.IsType<HtmlElementEventArgs>(e);
            callCount++;
        }

        validate();
        unsafe void validate()
        {
            document.ContextMenuShowing += handler;
            using var iHTMLDocument4 = ComHelpers.GetComScope<IHTMLDocument4>(document.DomDocument);
            using BSTR onContextMenu = new("oncontextmenu");
            VARIANT eventObj = default;
            VARIANT_BOOL cancelled = default;
            Assert.True(iHTMLDocument4.Value->fireEvent(onContextMenu, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);

            // Remove handler.
            document.ContextMenuShowing -= handler;
            Assert.True(iHTMLDocument4.Value->fireEvent(onContextMenu, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_Focusing_InvokeEvent_Success()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><p>InnerText</p></body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        int callCount = 0;
        void handler(object sender, EventArgs e)
        {
            Assert.Same(document, sender);
            Assert.IsType<HtmlElementEventArgs>(e);
            callCount++;
        }

        validate();
        unsafe void validate()
        {
            document.Focusing += handler;
            using var iHTMLDocument4 = ComHelpers.GetComScope<IHTMLDocument4>(document.DomDocument);
            using BSTR onFocusing = new("onfocusin");
            VARIANT eventObj = default;
            VARIANT_BOOL cancelled = default;
            Assert.True(iHTMLDocument4.Value->fireEvent(onFocusing, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);

            // Remove handler.
            document.Focusing -= handler;
            Assert.True(iHTMLDocument4.Value->fireEvent(onFocusing, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_LosingFocus_InvokeEvent_Success()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><p>InnerText</p></body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        int callCount = 0;
        void handler(object sender, EventArgs e)
        {
            Assert.Same(document, sender);
            Assert.IsType<HtmlElementEventArgs>(e);
            callCount++;
        }

        validate();
        unsafe void validate()
        {
            document.LosingFocus += handler;
            using var iHTMLDocument4 = ComHelpers.GetComScope<IHTMLDocument4>(document.DomDocument);
            using BSTR onFocusOut = new("onfocusout");
            VARIANT eventObj = default;
            VARIANT_BOOL cancelled = default;
            Assert.True(iHTMLDocument4.Value->fireEvent(onFocusOut, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);

            // Remove handler.
            document.LosingFocus -= handler;
            Assert.True(iHTMLDocument4.Value->fireEvent(onFocusOut, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_MouseDown_InvokeEvent_Success()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><p>InnerText</p></body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        int callCount = 0;
        void handler(object sender, EventArgs e)
        {
            Assert.Same(document, sender);
            Assert.IsType<HtmlElementEventArgs>(e);
            callCount++;
        }

        validate();
        unsafe void validate()
        {
            document.MouseDown += handler;
            using var iHTMLDocument4 = ComHelpers.GetComScope<IHTMLDocument4>(document.DomDocument);
            using BSTR onMouseDown = new("onmousedown");
            VARIANT eventObj = default;
            VARIANT_BOOL cancelled = default;
            Assert.True(iHTMLDocument4.Value->fireEvent(onMouseDown, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);

            // Remove handler.
            document.MouseDown -= handler;
            Assert.True(iHTMLDocument4.Value->fireEvent(onMouseDown, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_MouseLeave_InvokeEvent_Success()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><p>InnerText</p></body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        int callCount = 0;
        void handler(object sender, EventArgs e)
        {
            Assert.Same(document, sender);
            Assert.IsType<HtmlElementEventArgs>(e);
            callCount++;
        }

        validate();
        unsafe void validate()
        {
            document.MouseLeave += handler;
            using var iHTMLDocument4 = ComHelpers.GetComScope<IHTMLDocument4>(document.DomDocument);
            using BSTR onMouseOut = new("onmouseout");
            VARIANT eventObj = default;
            VARIANT_BOOL cancelled = default;
            Assert.True(iHTMLDocument4.Value->fireEvent(onMouseOut, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);

            // Remove handler.
            document.MouseLeave -= handler;
            Assert.True(iHTMLDocument4.Value->fireEvent(onMouseOut, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_MouseMove_InvokeEvent_Success()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><p>InnerText</p></body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        int callCount = 0;
        void handler(object sender, EventArgs e)
        {
            Assert.Same(document, sender);
            Assert.IsType<HtmlElementEventArgs>(e);
            callCount++;
        }

        validate();
        unsafe void validate()
        {
            document.MouseMove += handler;
            using var iHTMLDocument4 = ComHelpers.GetComScope<IHTMLDocument4>(document.DomDocument);
            using BSTR onMouseMove = new("onmousemove");
            VARIANT eventObj = default;
            VARIANT_BOOL cancelled = default;
            Assert.True(iHTMLDocument4.Value->fireEvent(onMouseMove, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);

            // Remove handler.
            document.MouseMove -= handler;
            Assert.True(iHTMLDocument4.Value->fireEvent(onMouseMove, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_MouseOver_InvokeEvent_Success()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><p>InnerText</p></body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        int callCount = 0;
        void handler(object sender, EventArgs e)
        {
            Assert.Same(document, sender);
            Assert.IsType<HtmlElementEventArgs>(e);
            callCount++;
        }

        validate();
        unsafe void validate()
        {
            document.MouseOver += handler;
            using var iHTMLDocument4 = ComHelpers.GetComScope<IHTMLDocument4>(document.DomDocument);
            using BSTR onMouseOver = new("onmouseover");
            VARIANT eventObj = default;
            VARIANT_BOOL cancelled = default;
            Assert.True(iHTMLDocument4.Value->fireEvent(onMouseOver, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);

            // Remove handler.
            document.MouseOver -= handler;
            Assert.True(iHTMLDocument4.Value->fireEvent(onMouseOver, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_MouseUp_InvokeEvent_Success()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><p>InnerText</p></body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        int callCount = 0;
        void handler(object sender, EventArgs e)
        {
            Assert.Same(document, sender);
            Assert.IsType<HtmlElementEventArgs>(e);
            callCount++;
        }

        validate();
        unsafe void validate()
        {
            document.MouseUp += handler;
            using var iHTMLDocument4 = ComHelpers.GetComScope<IHTMLDocument4>(document.DomDocument);
            using BSTR onMouseUp = new("onmouseup");
            VARIANT eventObj = default;
            VARIANT_BOOL cancelled = default;
            Assert.True(iHTMLDocument4.Value->fireEvent(onMouseUp, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);

            // Remove handler.
            document.MouseUp -= handler;
            Assert.True(iHTMLDocument4.Value->fireEvent(onMouseUp, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);
        }
    }

    [WinFormsFact]
    public async Task HtmlDocument_Stop_InvokeEvent_Success()
    {
        using Control parent = new();
        using WebBrowser control = new()
        {
            Parent = parent
        };

        const string Html = "<html><body><p>InnerText</p></body></html>";
        HtmlDocument document = await GetDocument(control, Html);

        int callCount = 0;
        void handler(object sender, EventArgs e)
        {
            Assert.Same(document, sender);
            Assert.IsType<HtmlElementEventArgs>(e);
            callCount++;
        }

        validate();
        unsafe void validate()
        {
            document.Stop += handler;
            using var iHTMLDocument4 = ComHelpers.GetComScope<IHTMLDocument4>(document.DomDocument);
            using BSTR onStop = new("onstop");
            VARIANT eventObj = default;
            VARIANT_BOOL cancelled = default;
            Assert.True(iHTMLDocument4.Value->fireEvent(onStop, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);

            // Remove handler.
            document.Stop -= handler;
            Assert.True(iHTMLDocument4.Value->fireEvent(onStop, &eventObj, &cancelled).Succeeded);
            Assert.Equal(1, callCount);
        }
    }

    private static async Task<HtmlDocument> GetDocument(WebBrowser control, string html)
    {
        TaskCompletionSource<bool> source = new();
        control.DocumentCompleted += (sender, e) => source.SetResult(true);

        using var file = CreateTempFile(html);
        await Task.Run(() => control.Navigate(file.Path));
        Assert.True(await source.Task);

        return control.Document;
    }

    private static TempFile CreateTempFile(string html)
    {
        byte[] data = Encoding.UTF8.GetBytes(html);
        return TempFile.Create(data);
    }
}
