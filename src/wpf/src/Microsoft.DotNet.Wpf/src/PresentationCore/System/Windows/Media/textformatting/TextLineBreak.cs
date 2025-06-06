// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//  Contents:  Text properties and state at the point where text is broken 
//             by the line breaking process, which may need to be carried over 
//             when formatting the next line.

using MS.Internal.TextFormatting;

namespace System.Windows.Media.TextFormatting
{
    /// <summary>
    /// Text properties and state at the point where text is broken
    /// by the line breaking process. 
    /// </summary>
    public sealed class TextLineBreak : IDisposable
    {
        private TextModifierScope  _currentScope;
        private IntPtr             _breakRecord;

        #region Constructors

        /// <summary>
        /// Internallly construct the line break
        /// </summary>
        internal TextLineBreak(
            TextModifierScope  currentScope,
            IntPtr             breakRecord
            )
        {
            _currentScope = currentScope;
            _breakRecord = breakRecord;

            if (breakRecord == IntPtr.Zero)
            {
                // this object does not hold unmanaged resource,
                // remove it from the finalizer queue.
                GC.SuppressFinalize(this);
            }
        }

        #endregion


        /// <summary>
        /// Finalize the line break
        /// </summary>
        ~TextLineBreak()
        {
            DisposeInternal(true);
        }


        /// <summary>
        /// Dispose the line break
        /// </summary>
        public void Dispose()
        {
            DisposeInternal(false);
            GC.SuppressFinalize(this);
        }


        /// <summary>
        /// Clone a new instance of TextLineBreak
        /// </summary>
        public TextLineBreak Clone()
        {
            IntPtr pbreakrec = IntPtr.Zero;

            if (_breakRecord != IntPtr.Zero)
            {
                LsErr lserr = UnsafeNativeMethods.LoCloneBreakRecord(_breakRecord, out pbreakrec);

                if (lserr != LsErr.None)
                {
                    TextFormatterContext.ThrowExceptionFromLsError(SR.Format(SR.CloneBreakRecordFailure, lserr), lserr);
                }
            }

            return new TextLineBreak(_currentScope, pbreakrec);
        }


        /// <summary>
        /// Destroy LS unmanaged break records object inside the line break 
        /// managed object. The parameter flag indicates whether the call is 
        /// from finalizer thread or the main UI thread.
        /// </summary>
        private void DisposeInternal(bool finalizing)
        {
            if (_breakRecord != IntPtr.Zero)
            {
                UnsafeNativeMethods.LoDisposeBreakRecord(_breakRecord, finalizing);

                _breakRecord = IntPtr.Zero;
                GC.KeepAlive(this);
            }
        }


        /// <summary>
        /// Current text modifier scope, which can be null.
        /// </summary>
        internal TextModifierScope TextModifierScope
        {
            get { return _currentScope; }
        }

        
        /// <summary>
        /// Unmanaged pointer to LS break records structure
        /// </summary>
        internal IntPtr BreakRecord
        {
            get { return _breakRecord; }
        }
    }
}
