// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace System.Xaml
{
    // provides a place to Write a list of Xaml nodes
    // and Read them back.  W/o exposing the 'XamlNode' type.

    // Single Writer, multiple reader.
    // Must complete writing and Close, before reading.

    public class XamlNodeList
    {
        private List<XamlNode> _nodeList;
        private bool _readMode;
        private XamlWriter _writer;
        private bool _hasLineInfo;

        public XamlNodeList(XamlSchemaContext schemaContext)
        {
            ArgumentNullException.ThrowIfNull(schemaContext);
            Initialize(schemaContext, 0);
        }

        public XamlNodeList(XamlSchemaContext schemaContext, int size)
        {
            ArgumentNullException.ThrowIfNull(schemaContext);
            Initialize(schemaContext, size);
        }

        private void Initialize(XamlSchemaContext schemaContext, int size)
        {
            if (size == 0)
            {
                _nodeList = new List<XamlNode>();
            }
            else
            {
                _nodeList = new List<XamlNode>(size);
            }

            _writer = new WriterDelegate(Add, AddLineInfo, schemaContext);
        }

        public XamlWriter Writer
        {
            get { return _writer; }
        }

        public XamlReader GetReader()
        {
            if (!_readMode)
            {
                throw new XamlException(SR.CloseXamlWriterBeforeReading);
            }

            if (_writer.SchemaContext is null)
            {
                throw new XamlException(SR.SchemaContextNotInitialized);
            }

            return new ReaderMultiIndexDelegate(_writer.SchemaContext, Index, _nodeList.Count, _hasLineInfo);
        }

        private void Add(XamlNodeType nodeType, object data)
        {
            if (!_readMode)
            {
                if (nodeType != XamlNodeType.None)
                {
                    XamlNode node = new XamlNode(nodeType, data);
                    _nodeList.Add(node);
                    return;
                }

                Debug.Assert(XamlNode.IsEof_Helper(nodeType, data));
                _readMode = true;
            }
            else
            {
                throw new XamlException(SR.CannotWriteClosedWriter);
            }
        }

        private void AddLineInfo(int lineNumber, int linePosition)
        {
            if (_readMode)
            {
                throw new XamlException(SR.CannotWriteClosedWriter);
            }

            XamlNode node = new XamlNode(new LineInfo(lineNumber, linePosition));
            _nodeList.Add(node);
            if (!_hasLineInfo)
            {
                _hasLineInfo = true;
            }
        }

        private XamlNode Index(int idx)
        {
            if (!_readMode)
            {
                throw new XamlException(SR.CloseXamlWriterBeforeReading);
            }

            return _nodeList[idx];
        }

        public void Clear()
        {
            _nodeList.Clear();
            _readMode = false;      // go back to write mode.
        }

        public int Count
        {
            get { return _nodeList.Count; }
        }
    }
}
