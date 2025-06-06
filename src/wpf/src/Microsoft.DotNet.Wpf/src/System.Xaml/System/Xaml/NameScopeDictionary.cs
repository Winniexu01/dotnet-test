﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections;
using System.Collections.Specialized;
using System.Windows.Markup;
using System.Xaml.MS.Impl;

namespace System.Xaml
{
    //
    // The implementation for this class is taken directly from the source of NameScope, including the use
    // of HybridDictionary to match the performance semantics of 3.0 for the time being
    // Note that the IEnumerable<T> uses KeyValuePair<string, object>
    // This means that we need to create KeyValuePairs on the fly
    // The other option would be to just use IEnumerable (or change the HybridDictionary to Dictionary<K,V>)
    // but I opted for generic usability for now since this shouldn't be a common hot path.
    internal class NameScopeDictionary : INameScopeDictionary
    {
        private HybridDictionary _nameMap;
        private INameScope _underlyingNameScope;
        private FrugalObjectList<string> _names;

        public NameScopeDictionary()
        {
        }

        public NameScopeDictionary(INameScope underlyingNameScope)
        {
            _names = new FrugalObjectList<string>();
            _underlyingNameScope = underlyingNameScope ?? throw new ArgumentNullException(nameof(underlyingNameScope));
        }

        public void RegisterName(string name, object scopedElement)
        {
            ArgumentNullException.ThrowIfNull(name);

            ArgumentNullException.ThrowIfNull(scopedElement);

            if (name.Length == 0)
                throw new ArgumentException(SR.NameScopeNameNotEmptyString);

            if (!NameValidationHelper.IsValidIdentifierName(name))
            {
                throw new ArgumentException(SR.Format(SR.NameScopeInvalidIdentifierName, name));
            }

            if (_underlyingNameScope is not null)
            {
                _names.Add(name);
                _underlyingNameScope.RegisterName(name, scopedElement);
            }
            else
            {
                if (_nameMap is null)
                {
                    _nameMap = new HybridDictionary();
                    _nameMap[name] = scopedElement;
                }
                else
                {
                    object nameContext = _nameMap[name];

                    if (nameContext is null)
                    {
                        _nameMap[name] = scopedElement;
                    }
                    else if (scopedElement != nameContext)
                    {
                        throw new ArgumentException(SR.Format(SR.NameScopeDuplicateNamesNotAllowed, name));
                    }
                }
            }
        }

        public void UnregisterName(string name)
        {
            ArgumentNullException.ThrowIfNull(name);

            if (name.Length == 0)
                throw new ArgumentException(SR.NameScopeNameNotEmptyString);

            if (_underlyingNameScope is not null)
            {
                _underlyingNameScope.UnregisterName(name);
                _names.Remove(name);
            }
            else
            {
                if (_nameMap is not null && _nameMap[name] is not null)
                {
                    _nameMap.Remove(name);
                }
                else
                {
                    throw new ArgumentException(SR.Format(SR.NameScopeNameNotFound, name));
                }
            }
        }

        public object FindName(string name)
        {
            ArgumentNullException.ThrowIfNull(name);

            if (name.Length == 0)
                throw new ArgumentException(SR.NameScopeNameNotEmptyString);

            if (_underlyingNameScope is not null)
            {
                return _underlyingNameScope.FindName(name);
            }
            else
            {
                if (_nameMap is null)
                {
                    return null;
                }

                return _nameMap[name];
            }
        }

        internal INameScope UnderlyingNameScope { get { return _underlyingNameScope; } }

        private class Enumerator : IEnumerator<KeyValuePair<string, object>>
        {
            private int index;
            private IDictionaryEnumerator dictionaryEnumerator;
            private HybridDictionary _nameMap;
            private INameScope _underlyingNameScope;
            private FrugalObjectList<string> _names;

            public Enumerator(NameScopeDictionary nameScopeDictionary)
            {
                _nameMap = nameScopeDictionary._nameMap;
                _underlyingNameScope = nameScopeDictionary._underlyingNameScope;
                _names = nameScopeDictionary._names;

                if (_underlyingNameScope is not null)
                {
                    index = -1;
                }
                else
                {
                    if (_nameMap is not null)
                    {
                        dictionaryEnumerator = _nameMap.GetEnumerator();
                    }
                }
            }

            public void Dispose()
            {
                GC.SuppressFinalize(this);
            }

            public KeyValuePair<string, object> Current
            {
                get
                {
                    if (_underlyingNameScope is not null)
                    {
                        string name = _names[index];
                        return new KeyValuePair<string, object>(name, _underlyingNameScope.FindName(name));
                    }
                    else
                    {
                        if (_nameMap is not null)
                        {
                            return new KeyValuePair<string, object>((string)dictionaryEnumerator.Key, dictionaryEnumerator.Value);
                        }

                        return default(KeyValuePair<string, object>);
                    }
                }
            }

            public bool MoveNext()
            {
                if (_underlyingNameScope is not null)
                {
                    if (index == _names.Count - 1)
                    {
                        return false;
                    }

                    index++;
                    return true;
                }
                else
                {
                    if (_nameMap is not null)
                    {
                        return dictionaryEnumerator.MoveNext();
                    }

                    return false;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }

            void IEnumerator.Reset()
            {
                if (_underlyingNameScope is not null)
                {
                    index = -1;
                }
                else
                {
                    dictionaryEnumerator.Reset();
                }
            }
        }

        private IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return new Enumerator(this);
        }

        #region IEnumerable methods
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion

        #region IEnumerable<KeyValuePair<string, object> methods
        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
        {
            return new Enumerator(this);
        }
        #endregion

        #region ICollection<KeyValuePair<string, object> methods
        int ICollection<KeyValuePair<string, object>>.Count
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        bool ICollection<KeyValuePair<string, object>>.IsReadOnly
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        void ICollection<KeyValuePair<string, object>>.Clear()
        {
            throw new NotImplementedException();
        }

        void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
        {
            throw new NotImplementedException();
        }

        void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
        {
            throw new NotImplementedException();
        }

        bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IDictionary<string, object> methods
        object IDictionary<string, object>.this[string key]
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        void IDictionary<string, object>.Add(string key, object value)
        {
            throw new NotImplementedException();
        }

        bool IDictionary<string, object>.ContainsKey(string key)
        {
            throw new NotImplementedException();
        }

        bool IDictionary<string, object>.Remove(string key)
        {
            throw new NotImplementedException();
        }

        bool IDictionary<string, object>.TryGetValue(string key, out object value)
        {
            throw new NotImplementedException();
        }

        ICollection<string> IDictionary<string, object>.Keys
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        ICollection<object> IDictionary<string, object>.Values
        {
            get
            {
                throw new NotImplementedException();
            }
        }
        #endregion
    }
}
