﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xaml;

namespace System.Windows
{
    public class TemplateContentLoader : XamlDeferringLoader
    {
        public override object Load(XamlReader xamlReader, IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(xamlReader);

            IXamlObjectWriterFactory factory = RequireService<IXamlObjectWriterFactory>(serviceProvider);
            return new TemplateContent(xamlReader, factory, serviceProvider);
        }

        private static T RequireService<T>(IServiceProvider provider) where T : class
        {
            T result = provider.GetService(typeof(T)) as T;
            if (result == null)
            {
                throw new InvalidOperationException(SR.Format(SR.DeferringLoaderNoContext, nameof(TemplateContentLoader), typeof(T).Name));
            }
            return result;
        }

        public override XamlReader Save(object value, IServiceProvider serviceProvider)
        {
            throw new NotSupportedException(SR.Format(SR.DeferringLoaderNoSave, nameof(TemplateContentLoader)));
        }
    }
}
