// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// OBJECT.CPP
//
// Definitions of a CLR Object
//

#include "common.h"

#include "vars.hpp"
#include "class.h"
#include "object.h"
#include "threads.h"
#include "excep.h"
#include "eeconfig.h"
#include "gcheaputilities.h"
#include "field.h"
#include "argdestination.h"


SVAL_IMPL(INT32, ArrayBase, s_arrayBoundsZero);

static DWORD GetGlobalNewHashCode()
{
    LIMITED_METHOD_CONTRACT;
    // Used for generating hash codes for exceptions to determine whether the
    // Catch_Handler_Found_Event should be reported. See Thread::GetNewHashCode.
    // Using linear congruential generator from Knuth Vol. 2, p. 102, line 24
    static DWORD dwHashCodeSeed = 123456789U * 1566083941U + 1;
    const DWORD multiplier = 1*4 + 5; //same as the GetNewHashCode method
    dwHashCodeSeed = dwHashCodeSeed*multiplier + 1;
    return dwHashCodeSeed;
}

// follow the necessary rules to get a new valid hashcode for an object
DWORD Object::ComputeHashCode()
{
    DWORD hashCode;

    // note that this algorithm now uses at most HASHCODE_BITS so that it will
    // fit into the objheader if the hashcode has to be moved back into the objheader
    // such as for an object that is being frozen
    Thread *pThread = GetThreadNULLOk();
    do
    {
        if (pThread == NULL)
        {
            hashCode = (GetGlobalNewHashCode() >> (32-HASHCODE_BITS));
        }
        else
        {
            // we use the high order bits in this case because they're more random
            hashCode = pThread->GetNewHashCode() >> (32-HASHCODE_BITS);
        }
    }
    while (hashCode == 0);   // need to enforce hashCode != 0

    // verify that it really fits into HASHCODE_BITS
     _ASSERTE((hashCode & ((1<<HASHCODE_BITS)-1)) == hashCode);

    return hashCode;
}

DWORD Object::GetGlobalNewHashCode()
{
    LIMITED_METHOD_CONTRACT;
    // Used for generating hash codes for exceptions to determine whether the
    // Catch_Handler_Found_Event should be reported. See Thread::GetNewHashCode.
    // Using linear congruential generator from Knuth Vol. 2, p. 102, line 24
    static DWORD dwHashCodeSeed = 123456789U * 1566083941U + 1;
    const DWORD multiplier = 1*4 + 5; //same as the GetNewHashCode method
    dwHashCodeSeed = dwHashCodeSeed*multiplier + 1;
    return dwHashCodeSeed;
}

#ifndef DACCESS_COMPILE
INT32 Object::GetHashCodeEx()
{
    CONTRACTL
    {
        MODE_COOPERATIVE;
        THROWS;
        GC_NOTRIGGER;
    }
    CONTRACTL_END

    // This loop exists because we're inspecting the header dword of the object
    // and it may change under us because of races with other threads.
    // On top of that, it may have the spin lock bit set, in which case we're
    // not supposed to change it.
    // In all of these case, we need to retry the operation.
    DWORD iter = 0;
    DWORD dwSwitchCount = 0;
    while (true)
    {
        DWORD bits = GetHeader()->GetBits();

        if (bits & BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX)
        {
            if (bits & BIT_SBLK_IS_HASHCODE)
            {
                // Common case: the object already has a hash code
                return  bits & MASK_HASHCODE;
            }
            else
            {
                // We have a sync block index. This means if we already have a hash code,
                // it is in the sync block, otherwise we generate a new one and store it there
                SyncBlock *psb = GetSyncBlock();
                DWORD hashCode = psb->GetHashCode();
                if (hashCode != 0)
                    return  hashCode;

                hashCode = ComputeHashCode();

                return psb->SetHashCode(hashCode);
            }
        }
        else
        {
            // If a thread is holding the thin lock we need a syncblock
            if ((bits & (SBLK_MASK_LOCK_THREADID)) != 0)
            {
                GetSyncBlock();
                // No need to replicate the above code dealing with sync blocks
                // here - in the next iteration of the loop, we'll realize
                // we have a syncblock, and we'll do the right thing.
            }
            else
            {
                // We want to change the header in this case, so we have to check the BIT_SBLK_SPIN_LOCK bit first
                if (bits & BIT_SBLK_SPIN_LOCK)
                {
                    iter++;
                    if ((iter % 1024) != 0 && g_SystemInfo.dwNumberOfProcessors > 1)
                    {
                        YieldProcessorNormalized(); // indicate to the processor that we are spinning
                    }
                    else
                    {
                        __SwitchToThread(0, ++dwSwitchCount);
                    }
                    continue;
                }

                DWORD hashCode = ComputeHashCode();

                DWORD newBits = bits | BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX | BIT_SBLK_IS_HASHCODE | hashCode;

                if (GetHeader()->SetBits(newBits, bits) == bits)
                    return hashCode;
                // Header changed under us - let's restart this whole thing.
            }
        }
    }
}
#endif // #ifndef DACCESS_COMPILE

BOOL Object::ValidateObjectWithPossibleAV()
{
    CANNOT_HAVE_CONTRACT;
    SUPPORTS_DAC;

    PTR_MethodTable table = GetGCSafeMethodTable();
    if (table == NULL)
    {
        return FALSE;
    }

    return table->ValidateWithPossibleAV();
}


#ifndef DACCESS_COMPILE

// There are cases where it is not possible to get a type handle during a GC.
// If we can get the type handle, this method will return it.
// Otherwise, the method will return NULL.
TypeHandle Object::GetGCSafeTypeHandleIfPossible() const
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        if(!IsGCThread()) { MODE_COOPERATIVE; }
    }
    CONTRACTL_END;

    // Although getting the type handle is unsafe and could cause recursive type lookups
    // in some cases, it's always safe and straightforward to get to the MethodTable.
    MethodTable * pMT = GetGCSafeMethodTable();
    _ASSERTE(pMT != NULL);

    if (pMT == g_pFreeObjectMethodTable)
    {
        return NULL;
    }

    // Don't look at types that belong to an unloading AppDomain, or else
    // pObj->GetGCSafeTypeHandle() can AV. For example, we encountered this AV when pObj
    // was an array like this:
    //
    //     MyValueType1<MyValueType2>[] myArray
    //
    // where MyValueType1<T> & MyValueType2 are defined in different assemblies. In such
    // a case, looking up the type handle for myArray requires looking in
    // MyValueType1<T>'s module's m_AssemblyRefByNameTable, which is garbage if its
    // AppDomain is unloading.
    //
    // Another AV was encountered in a similar case,
    //
    //     MyRefType1<MyRefType2>[] myArray
    //
    // where MyRefType2's module was unloaded by the time the GC occurred. In at least
    // one case, the GC was caused by the AD unload itself (AppDomain::Unload ->
    // AppDomain::Exit -> GCInterface::AddMemoryPressure -> WKS::GCHeapUtilities::GarbageCollect).
    //
    // To protect against all scenarios, verify that
    //
    //     * The MT of the object is not getting unloaded, OR
    //     * In the case of arrays (potentially of arrays of arrays of arrays ...), the
    //         MT of the innermost element is not getting unloaded. This then ensures the
    //         MT of the original object (i.e., array) itself must not be getting
    //         unloaded either, since the MTs of arrays and of their elements are
    //         allocated on the same loader allocator.
    Module * pLoaderModule = pMT->GetLoaderModule();

    // Don't look up types that are unloading due to Collectible Assemblies. Haven't been
    // able to find a case where we actually encounter objects like this that can cause
    // problems; however, it seems prudent to add this protection just in case.
    LoaderAllocator * pLoaderAllocator = pLoaderModule->GetLoaderAllocator();
    _ASSERTE(pLoaderAllocator != NULL);
    if ((pLoaderAllocator->IsCollectible()) &&
        (ObjectHandleIsNull(pLoaderAllocator->GetLoaderAllocatorObjectHandle())))
    {
        return NULL;
    }

    // Ok, it should now be safe to get the type handle
    return GetGCSafeTypeHandle();
}

/* static */ BOOL Object::SupportsInterface(OBJECTREF pObj, MethodTable* pInterfaceMT)
{
    CONTRACTL
    {
        THROWS;
        GC_TRIGGERS;
        INJECT_FAULT(COMPlusThrowOM());
        PRECONDITION(CheckPointer(pInterfaceMT));
        PRECONDITION(pInterfaceMT->IsInterface());
    }
    CONTRACTL_END

    BOOL bSupportsItf = FALSE;

    GCPROTECT_BEGIN(pObj)
    {
        // Make sure the interface method table has been restored.
        pInterfaceMT->CheckRestore();

        // Check to see if the static class definition indicates we implement the interface.
        MethodTable * pMT = pObj->GetMethodTable();
        if (pMT->CanCastToInterface(pInterfaceMT))
        {
            bSupportsItf = TRUE;
        }
#ifdef FEATURE_COMINTEROP
        else
        if (pMT->IsComObjectType())
        {
            // If this is a COM object, the static class definition might not be complete so we need
            // to check if the COM object implements the interface.
            bSupportsItf = ComObject::SupportsInterface(pObj, pInterfaceMT);
        }
#endif // FEATURE_COMINTEROP
    }
    GCPROTECT_END();

    return bSupportsItf;
}

Assembly *AssemblyBaseObject::GetAssembly()
{
    WRAPPER_NO_CONTRACT;
    return m_pAssembly;
}

STRINGREF AllocateString(SString sstr)
{
    CONTRACTL {
        THROWS;
        GC_TRIGGERS;
    } CONTRACTL_END;

    COUNT_T length = sstr.GetCount(); // count of WCHARs excluding terminating NULL
    STRINGREF strObj = AllocateString(length);
    memcpyNoGCRefs(strObj->GetBuffer(), sstr.GetUnicode(), length*sizeof(WCHAR));

    return strObj;
}

void Object::ValidateHeap(BOOL bDeep)
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;

#if defined (VERIFY_HEAP)
    //no need to verify next object's header in this case
    //since this is called in verify_heap, which will verfiy every object anyway
    Validate(bDeep, FALSE);
#endif
}

void Object::SetOffsetObjectRef(DWORD dwOffset, size_t dwValue)
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;
    STATIC_CONTRACT_MODE_COOPERATIVE;

    OBJECTREF*  location;
    OBJECTREF   o;

    location = (OBJECTREF *) &GetData()[dwOffset];
    o        = ObjectToOBJECTREF(*(Object **)  &dwValue);

    SetObjectReference( location, o );
}

void SetObjectReferenceUnchecked(OBJECTREF *dst,OBJECTREF ref)
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;
    STATIC_CONTRACT_MODE_COOPERATIVE;
    STATIC_CONTRACT_CANNOT_TAKE_LOCK;

    // Assign value. We use casting to avoid going thru the overloaded
    // OBJECTREF= operator which in this case would trigger a false
    // write-barrier violation assert.
    VolatileStore((Object**)dst, OBJECTREFToObject(ref));
#ifdef _DEBUG
    Thread::ObjectRefAssign(dst);
#endif
    ErectWriteBarrier(dst, ref);
}

void CopyValueClassUnchecked(void* dest, void* src, MethodTable *pMT)
{

    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;
    STATIC_CONTRACT_MODE_COOPERATIVE;

    _ASSERTE(!pMT->IsArray());  // bunch of assumptions about arrays wrong.

    if (pMT->ContainsGCPointers())
    {
        memmoveGCRefs(dest, src, pMT->GetNumInstanceFieldBytesIfContainsGCPointers());
    }
    else
    {
        DWORD numInstanceFieldBytes = pMT->GetNumInstanceFieldBytes();
        switch (numInstanceFieldBytes)
        {
        case 1:
            *(UINT8*)dest = *(UINT8*)src;
            break;
#ifndef ALIGN_ACCESS
            // we can hit an alignment fault if the value type has multiple
            // smaller fields.  Example: if there are two I4 fields, the
            // value class can be aligned to 4-byte boundaries, yet the
            // NumInstanceFieldBytes is 8
        case 2:
            *(UINT16*)dest = *(UINT16*)src;
            break;
        case 4:
            *(UINT32*)dest = *(UINT32*)src;
            break;
        case 8:
            *(UINT64*)dest = *(UINT64*)src;
            break;
#endif // !ALIGN_ACCESS
        default:
            memcpyNoGCRefs(dest, src, numInstanceFieldBytes);
            break;
        }
    }
}

// Copy value class into the argument specified by the argDest.
// The destOffset is nonzero when copying values into Nullable<T>, it is the offset
// of the T value inside of the Nullable<T>
void CopyValueClassArgUnchecked(ArgDestination *argDest, void* src, MethodTable *pMT, int destOffset)
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;
    STATIC_CONTRACT_MODE_COOPERATIVE;

#if defined(UNIX_AMD64_ABI)

    if (argDest->IsStructPassedInRegs())
    {
        argDest->CopyStructToRegisters(src, pMT->GetNumInstanceFieldBytes(), destOffset);
        return;
    }

#elif defined(TARGET_ARM64)

    if (argDest->IsHFA())
    {
        argDest->CopyHFAStructToRegister(src, pMT->GetNumInstanceFieldBytes());
        return;
    }

#elif defined(TARGET_LOONGARCH64) || defined(TARGET_RISCV64)

    if (argDest->IsStructPassedInRegs())
    {
        argDest->CopyStructToRegisters(src, pMT->GetNumInstanceFieldBytes(), destOffset);
        return;
    }

#endif // UNIX_AMD64_ABI
    // destOffset is only valid for Nullable<T> passed in registers
    _ASSERTE(destOffset == 0);

    CopyValueClassUnchecked(argDest->GetDestinationAddress(), src, pMT);
}

// Initialize the value class argument to zeros
void InitValueClassArg(ArgDestination *argDest, MethodTable *pMT)
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;
    STATIC_CONTRACT_MODE_COOPERATIVE;

#if defined(UNIX_AMD64_ABI)

    if (argDest->IsStructPassedInRegs())
    {
        argDest->ZeroStructInRegisters(pMT->GetNumInstanceFieldBytes());
        return;
    }

#endif

#if defined(TARGET_LOONGARCH64) || defined(TARGET_RISCV64)
    if (argDest->IsStructPassedInRegs())
    {
        *(UINT64*)(argDest->GetStructGenRegDestinationAddress()) = 0;
        *(UINT64*)(argDest->GetDestinationAddress()) = 0;
        return;
    }
#endif

    InitValueClass(argDest->GetDestinationAddress(), pMT);
}

#if defined (VERIFY_HEAP)

#include "dbginterface.h"

    // make the checking code goes as fast as possible!
#if defined(_MSC_VER)
#pragma optimize("tgy", on)
#endif

#define CREATE_CHECK_STRING(x) #x
#define CHECK_AND_TEAR_DOWN(x)                                      \
    do{                                                             \
        if (!(x))                                                   \
        {                                                           \
            _ASSERTE(!CREATE_CHECK_STRING(x));                      \
            EEPOLICY_HANDLE_FATAL_ERROR(COR_E_EXECUTIONENGINE);     \
        }                                                           \
    } while (0)

VOID Object::Validate(BOOL bDeep, BOOL bVerifyNextHeader, BOOL bVerifySyncBlock)
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;
    STATIC_CONTRACT_MODE_COOPERATIVE;
    STATIC_CONTRACT_CANNOT_TAKE_LOCK;

    if (g_fEEShutDown & ShutDown_Phase2)
    {
        // During second phase of shutdown the code below is not guaranteed to work.
        return;
    }

#ifdef _DEBUG
    {
        Thread *pThread = GetThreadNULLOk();

        if (pThread != NULL && !(pThread->PreemptiveGCDisabled()))
        {
            // Debugger helper threads are special in that they take over for
            // what would normally be a nonEE thread (the RCThread).  If an
            // EE thread is doing RCThread duty, then it should be treated
            // as such.
            //
            // There are some GC threads in the same kind of category.  Note that
            // GetThread() sometimes returns them, if DLL_THREAD_ATTACH notifications
            // have run some managed code.
            if (!dbgOnly_IsSpecialEEThread() && !IsGCSpecialThread())
                _ASSERTE(!"OBJECTREF being accessed while thread is in preemptive GC mode.");
        }
    }
#endif


    {   // ValidateInner can throw or fault on failure which violates contract.
        CONTRACT_VIOLATION(ThrowsViolation | FaultViolation);

        // using inner helper because of TRY and stack objects with destructors.
        ValidateInner(bDeep, bVerifyNextHeader, bVerifySyncBlock);
    }
}

VOID Object::ValidateInner(BOOL bDeep, BOOL bVerifyNextHeader, BOOL bVerifySyncBlock)
{
    STATIC_CONTRACT_THROWS; // See CONTRACT_VIOLATION above
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FAULT; // See CONTRACT_VIOLATION above
    STATIC_CONTRACT_MODE_COOPERATIVE;
    STATIC_CONTRACT_CANNOT_TAKE_LOCK;

    int lastTest = 0;

    EX_TRY
    {
        // in order to avoid contract violations in the EH code we'll allow AVs here,
        // they'll be handled in the catch block
        AVInRuntimeImplOkayHolder avOk;

        MethodTable *pMT = GetGCSafeMethodTable();

        lastTest = 1;

        CHECK_AND_TEAR_DOWN(pMT && pMT->Validate());
        lastTest = 2;

        bool noRangeChecks =
            (g_pConfig->GetHeapVerifyLevel() & EEConfig::HEAPVERIFY_NO_RANGE_CHECKS) == EEConfig::HEAPVERIFY_NO_RANGE_CHECKS;

        // noRangeChecks depends on initial values being FALSE
        BOOL bSmallObjectHeapPtr = FALSE, bLargeObjectHeapPtr = FALSE;
        if (!noRangeChecks)
        {
            bSmallObjectHeapPtr = GCHeapUtilities::GetGCHeap()->IsHeapPointer(this, true);
            if (!bSmallObjectHeapPtr)
                bLargeObjectHeapPtr = GCHeapUtilities::GetGCHeap()->IsHeapPointer(this);

            CHECK_AND_TEAR_DOWN(bSmallObjectHeapPtr || bLargeObjectHeapPtr);
        }

        lastTest = 3;

        if (bDeep)
        {
            CHECK_AND_TEAR_DOWN(GetHeader()->Validate(bVerifySyncBlock));
        }

        lastTest = 4;

        if (bDeep && (g_pConfig->GetHeapVerifyLevel() & EEConfig::HEAPVERIFY_GC)) {
            GCHeapUtilities::GetGCHeap()->ValidateObjectMember(this);
        }

        lastTest = 5;

        // since bSmallObjectHeapPtr is initialized to FALSE
        // we skip checking noRangeChecks since if skipping
        // is enabled bSmallObjectHeapPtr will always be false.
        if (bSmallObjectHeapPtr) {
            CHECK_AND_TEAR_DOWN(!GCHeapUtilities::GetGCHeap()->IsLargeObject(this));
        }

        lastTest = 6;

        lastTest = 7;

        _ASSERTE(GCHeapUtilities::IsGCHeapInitialized());
        // try to validate next object's header
        if (bDeep
            && bVerifyNextHeader
            && GCHeapUtilities::GetGCHeap()->RuntimeStructuresValid()
            //NextObj could be very slow if concurrent GC is going on
            && !GCHeapUtilities::GetGCHeap ()->IsConcurrentGCInProgress ())
        {
            Object * nextObj = GCHeapUtilities::GetGCHeap ()->NextObj (this);
            if ((nextObj != NULL) &&
                (nextObj->GetGCSafeMethodTable() != nullptr) &&
                (nextObj->GetGCSafeMethodTable() != g_pFreeObjectMethodTable))
            {
                // we need a read barrier here - to make sure we read the object header _after_
                // reading data that tells us that the object is eligible for verification
                // (also see: gc.cpp/a_fit_segment_end_p)
                VOLATILE_MEMORY_BARRIER();
                CHECK_AND_TEAR_DOWN(nextObj->GetHeader()->Validate(FALSE));
            }
        }

        lastTest = 8;

#ifdef FEATURE_64BIT_ALIGNMENT
        if (pMT->RequiresAlign8())
        {
            CHECK_AND_TEAR_DOWN((((size_t)this) & 0x7) == (size_t)(pMT->IsValueType()?4:0));
        }
        lastTest = 9;
#endif // FEATURE_64BIT_ALIGNMENT

    }
    EX_CATCH
    {
        STRESS_LOG3(LF_ASSERT, LL_ALWAYS, "Detected use of corrupted OBJECTREF: %p [MT=%p] (lastTest=%d)", this, lastTest > 0 ? (*(size_t*)this) : 0, lastTest);
        CHECK_AND_TEAR_DOWN(!"Detected use of a corrupted OBJECTREF. Possible GC hole.");
    }
    EX_END_CATCH
}


#endif   // VERIFY_HEAP

/*==================================NewString===================================
**Action:  Creates a System.String object.
**Returns:
**Arguments:
**Exceptions:
==============================================================================*/
STRINGREF StringObject::NewString(INT32 length) {
    CONTRACTL {
        GC_TRIGGERS;
        MODE_COOPERATIVE;
        PRECONDITION(length>=0);
    } CONTRACTL_END;

    STRINGREF pString;

    if (length<0) {
        return NULL;
    } else if (length == 0) {
        return GetEmptyString();
    } else {
        pString = AllocateString(length);
        _ASSERTE(pString->GetBuffer()[length] == 0);

        return pString;
    }
}


/*==================================NewString===================================
**Action: Many years ago, VB didn't have the concept of a byte array, so enterprising
**        users created one by allocating a BSTR with an odd length and using it to
**        store bytes.  A generation later, we're still stuck supporting this behavior.
**        The way that we do this is to take advantage of the difference between the
**        array length and the string length.  The string length will always be the
**        number of characters between the start of the string and the terminating 0.
**        If we need an odd number of bytes, we'll take one wchar after the terminating 0.
**        (e.g. at position StringLength+1).  The high-order byte of this wchar is
**        reserved for flags and the low-order byte is our odd byte. This function is
**        used to allocate a string of that shape, but we don't actually mark the
**        trailing byte as being in use yet.
**Returns: A newly allocated string.  Null if length is less than 0.
**Arguments: length -- the length of the string to allocate
**           bHasTrailByte -- whether the string also has a trailing byte.
**Exceptions: OutOfMemoryException if AllocateString fails.
==============================================================================*/
STRINGREF StringObject::NewString(INT32 length, BOOL bHasTrailByte) {
    CONTRACTL {
        GC_TRIGGERS;
        MODE_COOPERATIVE;
        PRECONDITION(length>=0 && length != INT32_MAX);
    } CONTRACTL_END;

    STRINGREF pString;
    if (length<0 || length == INT32_MAX) {
        return NULL;
    } else if (length == 0) {
        return GetEmptyString();
    } else {
        pString = AllocateString(length);
        _ASSERTE(pString->GetBuffer()[length]==0);
        if (bHasTrailByte) {
            _ASSERTE(pString->GetBuffer()[length+1]==0);
        }
    }

    return pString;
}

//========================================================================
// Creates a System.String object and initializes from
// the supplied null-terminated C string.
//
// Maps NULL to null. This function does *not* return null to indicate
// error situations: it throws an exception instead.
//========================================================================
STRINGREF StringObject::NewString(const WCHAR *pwsz)
{
    CONTRACTL {
        GC_TRIGGERS;
        MODE_COOPERATIVE;
    } CONTRACTL_END;

    if (!pwsz)
    {
        return NULL;
    }
    else
    {

        DWORD nch = (DWORD)u16_strlen(pwsz);
        if (nch==0) {
            return GetEmptyString();
        }

#if 0
        //
        // This assert is disabled because it is valid for us to get a
        // pointer from the gc heap here as long as it is pinned.  This
        // can happen when a string is marshalled to unmanaged by
        // pinning and then later put into a struct and that struct is
        // then marshalled to managed.
        //
        _ASSERTE(!GCHeapUtilities::GetGCHeap()->IsHeapPointer((BYTE *) pwsz) ||
                 !"pwsz can not point to GC Heap");
#endif // 0

        STRINGREF pString = AllocateString( nch );

        memcpyNoGCRefs(pString->GetBuffer(), pwsz, nch*sizeof(WCHAR));
        _ASSERTE(pString->GetBuffer()[nch] == 0);
        return pString;
    }
}

#if defined(_MSC_VER) && defined(TARGET_X86)
#pragma optimize("y", on)        // Small critical routines, don't put in EBP frame
#endif

STRINGREF StringObject::NewString(const WCHAR *pwsz, int length) {
    CONTRACTL {
        THROWS;
        GC_TRIGGERS;
        MODE_COOPERATIVE;
        PRECONDITION(length>=0);
    } CONTRACTL_END;

    if (!pwsz)
    {
        return NULL;
    }
    else if (length <= 0) {
        return GetEmptyString();
    } else {
#if 0
        //
        // This assert is disabled because it is valid for us to get a
        // pointer from the gc heap here as long as it is pinned.  This
        // can happen when a string is marshalled to unmanaged by
        // pinning and then later put into a struct and that struct is
        // then marshalled to managed.
        //
        _ASSERTE(!GCHeapUtilities::GetGCHeap()->IsHeapPointer((BYTE *) pwsz) ||
                 !"pwsz can not point to GC Heap");
#endif // 0
        STRINGREF pString = AllocateString(length);

        memcpyNoGCRefs(pString->GetBuffer(), pwsz, length*sizeof(WCHAR));
        _ASSERTE(pString->GetBuffer()[length] == 0);
        return pString;
    }
}

#if defined(_MSC_VER) && defined(TARGET_X86)
#pragma optimize("", on)        // Go back to command line default optimizations
#endif

STRINGREF StringObject::NewString(LPCUTF8 psz)
{
    CONTRACTL {
        GC_TRIGGERS;
        MODE_COOPERATIVE;
        THROWS;
        PRECONDITION(CheckPointer(psz));
    } CONTRACTL_END;

    int length = (int)strlen(psz);
    if (length == 0) {
        return GetEmptyString();
    }
    CQuickBytes qb;
    WCHAR* pwsz = (WCHAR*) qb.AllocThrows((length) * sizeof(WCHAR));
    length = MultiByteToWideChar(CP_UTF8, 0, psz, length, pwsz, length);
    if (length == 0) {
        COMPlusThrow(kArgumentException, W("Arg_InvalidUTF8String"));
    }
    return NewString(pwsz, length);
}

STRINGREF StringObject::NewString(LPCUTF8 psz, int cBytes)
{
    CONTRACTL {
        GC_TRIGGERS;
        MODE_COOPERATIVE;
        THROWS;
        PRECONDITION(CheckPointer(psz, NULL_OK));
    } CONTRACTL_END;

    if (!psz)
        return NULL;

    _ASSERTE(psz);
    _ASSERTE(cBytes >= 0);
    if (cBytes == 0) {
        return GetEmptyString();
    }
    int cWszBytes = 0;
    if (!ClrSafeInt<int>::multiply(cBytes, sizeof(WCHAR), cWszBytes))
        COMPlusThrowOM();
    CQuickBytes qb;
    WCHAR* pwsz = (WCHAR*) qb.AllocThrows(cWszBytes);
    int length = MultiByteToWideChar(CP_UTF8, 0, psz, cBytes, pwsz, cBytes);
    if (length == 0) {
        COMPlusThrow(kArgumentException, W("Arg_InvalidUTF8String"));
    }
    return NewString(pwsz, length);
}

//
//
// STATIC MEMBER VARIABLES
//
//
STRINGREF* StringObject::EmptyStringRefPtr = NULL;
bool StringObject::EmptyStringIsFrozen = false;

//The special string helpers are used as flag bits for weird strings that have bytes
//after the terminating 0.  The only case where we use this right now is the VB BSTR as
//byte array which is described in MakeStringAsByteArrayFromBytes.
#define SPECIAL_STRING_VB_BYTE_ARRAY 0x100

FORCEINLINE BOOL MARKS_VB_BYTE_ARRAY(WCHAR x)
{
    return static_cast<BOOL>(x & SPECIAL_STRING_VB_BYTE_ARRAY);
}

FORCEINLINE WCHAR MAKE_VB_TRAIL_BYTE(BYTE x)
{
    return static_cast<WCHAR>(x) | SPECIAL_STRING_VB_BYTE_ARRAY;
}

FORCEINLINE BYTE GET_VB_TRAIL_BYTE(WCHAR x)
{
    return static_cast<BYTE>(x & 0xFF);
}


/*==============================InitEmptyStringRefPtr============================
**Action:  Gets an empty string refptr, cache the result.
**Returns: The retrieved STRINGREF.
==============================================================================*/
STRINGREF* StringObject::InitEmptyStringRefPtr() {
    CONTRACTL {
        THROWS;
        MODE_ANY;
        GC_TRIGGERS;
    } CONTRACTL_END;

    GCX_COOP();

    EEStringData data(0, W(""), TRUE);
    void* pinnedStr = nullptr;
    EmptyStringRefPtr = SystemDomain::System()->DefaultDomain()->GetLoaderAllocator()->GetStringObjRefPtrFromUnicodeString(&data, &pinnedStr);
    EmptyStringIsFrozen = pinnedStr != nullptr;
    return EmptyStringRefPtr;
}

/*============================InternalTrailByteCheck============================
**Action: Many years ago, VB didn't have the concept of a byte array, so enterprising
**        users created one by allocating a BSTR with an odd length and using it to
**        store bytes.  A generation later, we're still stuck supporting this behavior.
**        The way that we do this is stick the trail byte in the sync block
**        whenever we encounter such a situation. Since we expect this to be a very corner case
**        accessing the sync block seems like a good enough solution
**
**Returns: True if <CODE>str</CODE> contains a VB trail byte, false otherwise.
**Arguments: str -- The string to be examined.
**Exceptions: None
==============================================================================*/
BOOL StringObject::HasTrailByte() {
    WRAPPER_NO_CONTRACT;

    SyncBlock * pSyncBlock = PassiveGetSyncBlock();
    if(pSyncBlock != NULL)
    {
        return pSyncBlock->HasCOMBstrTrailByte();
    }

    return FALSE;
}

/*=================================GetTrailByte=================================
**Action:  If <CODE>str</CODE> contains a vb trail byte, returns a copy of it.
**Returns: True if <CODE>str</CODE> contains a trail byte.  *bTrailByte is set to
**         the byte in question if <CODE>str</CODE> does have a trail byte, otherwise
**         it's set to 0.
**Arguments: str -- The string being examined.
**           bTrailByte -- An out param to hold the value of the trail byte.
**Exceptions: None.
==============================================================================*/
BOOL StringObject::GetTrailByte(BYTE *bTrailByte) {
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_ANY;
    }
    CONTRACTL_END;
    _ASSERTE(bTrailByte);
    *bTrailByte=0;

    BOOL retValue = HasTrailByte();

    if(retValue)
    {
        *bTrailByte = GET_VB_TRAIL_BYTE(GetHeader()->PassiveGetSyncBlock()->GetCOMBstrTrailByte());
    }

    return retValue;
}

/*=================================SetTrailByte=================================
**Action: Sets the trail byte in the sync block
**Returns: True.
**Arguments: str -- The string into which to set the trail byte.
**           bTrailByte -- The trail byte to be added to the string.
**Exceptions: None.
==============================================================================*/
BOOL StringObject::SetTrailByte(BYTE bTrailByte) {
    WRAPPER_NO_CONTRACT;

    GetHeader()->GetSyncBlock()->SetCOMBstrTrailByte(MAKE_VB_TRAIL_BYTE(bTrailByte));
    return TRUE;
}

#ifdef USE_CHECKED_OBJECTREFS

//-------------------------------------------------------------
// Default constructor, for non-initializing declarations:
//
//      OBJECTREF or;
//-------------------------------------------------------------
OBJECTREF::OBJECTREF()
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;

    m_asObj = (Object*)POISONC;
    Thread::ObjectRefNew(this);
}

//-------------------------------------------------------------
// Copy constructor, for passing OBJECTREF's as function arguments.
//-------------------------------------------------------------
OBJECTREF::OBJECTREF(const OBJECTREF & objref)
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_MODE_COOPERATIVE;
    STATIC_CONTRACT_FORBID_FAULT;

    VALIDATEOBJECT(objref.m_asObj);

    // !!! If this assert is fired, there are two possibilities:
    // !!! 1.  You are doing a type cast, e.g.  *(OBJECTREF*)pObj
    // !!!     Instead, you should use ObjectToOBJECTREF(*(Object**)pObj),
    // !!!                          or ObjectToSTRINGREF(*(StringObject**)pObj)
    // !!! 2.  There is a real GC hole here.
    // !!! Either way you need to fix the code.
    _ASSERTE(Thread::IsObjRefValid(&objref));
    if ((objref.m_asObj != 0) &&
        ((IGCHeap*)GCHeapUtilities::GetGCHeap())->IsHeapPointer( (BYTE*)this ))
    {
        _ASSERTE(!"Write Barrier violation. Must use SetObjectReference() to assign OBJECTREF's into the GC heap!");
    }
    m_asObj = objref.m_asObj;

    if (m_asObj != 0) {
        ENABLESTRESSHEAP();
    }

    Thread::ObjectRefNew(this);
}


//-------------------------------------------------------------
// VolatileLoadWithoutBarrier constructor
//-------------------------------------------------------------
OBJECTREF::OBJECTREF(const OBJECTREF *pObjref, tagVolatileLoadWithoutBarrier tag)
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_MODE_COOPERATIVE;
    STATIC_CONTRACT_FORBID_FAULT;

    Object* objrefAsObj = VolatileLoadWithoutBarrier(&pObjref->m_asObj);
    VALIDATEOBJECT(objrefAsObj);

    // !!! If this assert is fired, there are two possibilities:
    // !!! 1.  You are doing a type cast, e.g.  *(OBJECTREF*)pObj
    // !!!     Instead, you should use ObjectToOBJECTREF(*(Object**)pObj),
    // !!!                          or ObjectToSTRINGREF(*(StringObject**)pObj)
    // !!! 2.  There is a real GC hole here.
    // !!! Either way you need to fix the code.
    _ASSERTE(Thread::IsObjRefValid(pObjref));
    if ((objrefAsObj != 0) &&
        ((IGCHeap*)GCHeapUtilities::GetGCHeap())->IsHeapPointer( (BYTE*)this ))
    {
        _ASSERTE(!"Write Barrier violation. Must use SetObjectReference() to assign OBJECTREF's into the GC heap!");
    }
    m_asObj = objrefAsObj;

    if (m_asObj != 0) {
        ENABLESTRESSHEAP();
    }

    Thread::ObjectRefNew(this);
}


//-------------------------------------------------------------
// To allow NULL to be used as an OBJECTREF.
//-------------------------------------------------------------
OBJECTREF::OBJECTREF(TADDR nul)
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;

    //_ASSERTE(nul == 0);
    m_asObj = (Object*)nul;
    if( m_asObj != NULL)
    {
        // REVISIT_TODO: fix this, why is this constructor being used for non-null object refs?
        STATIC_CONTRACT_VIOLATION(ModeViolation);

        VALIDATEOBJECT(m_asObj);
        ENABLESTRESSHEAP();
    }
    Thread::ObjectRefNew(this);
}

//-------------------------------------------------------------
// This is for the GC's use only. Non-GC code should never
// use the "Object" class directly. The unused "int" argument
// prevents C++ from using this to implicitly convert Object*'s
// to OBJECTREF.
//-------------------------------------------------------------
OBJECTREF::OBJECTREF(Object *pObject)
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_MODE_COOPERATIVE;
    STATIC_CONTRACT_FORBID_FAULT;

    DEBUG_ONLY_FUNCTION;

    if ((pObject != 0) &&
        ((IGCHeap*)GCHeapUtilities::GetGCHeap())->IsHeapPointer( (BYTE*)this ))
    {
        _ASSERTE(!"Write Barrier violation. Must use SetObjectReference() to assign OBJECTREF's into the GC heap!");
    }
    m_asObj = pObject;
    VALIDATEOBJECT(m_asObj);
    if (m_asObj != 0) {
        ENABLESTRESSHEAP();
    }
    Thread::ObjectRefNew(this);
}

void OBJECTREF::Validate(BOOL bDeep, BOOL bVerifyNextHeader, BOOL bVerifySyncBlock)
{
    LIMITED_METHOD_CONTRACT;
    if (m_asObj)
    {
        m_asObj->Validate(bDeep, bVerifyNextHeader, bVerifySyncBlock);
    }
}

//-------------------------------------------------------------
// Test against NULL.
//-------------------------------------------------------------
int OBJECTREF::operator!() const
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;

    // We don't do any validation here, as we want to allow zero comparison in preemptive mode
    return !m_asObj;
}

//-------------------------------------------------------------
// Compare two OBJECTREF's.
//-------------------------------------------------------------
int OBJECTREF::operator==(const OBJECTREF &objref) const
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;

    if (objref.m_asObj != NULL) // Allow comparison to zero in preemptive mode
    {
        // REVISIT_TODO: Weakening the contract system a little bit here. We should really
        // add a special NULLOBJECTREF which can be used for these situations and have
        // a separate code path for that with the correct contract protections.
        STATIC_CONTRACT_VIOLATION(ModeViolation);

        VALIDATEOBJECT(objref.m_asObj);

        // !!! If this assert is fired, there are two possibilities:
        // !!! 1.  You are doing a type cast, e.g.  *(OBJECTREF*)pObj
        // !!!     Instead, you should use ObjectToOBJECTREF(*(Object**)pObj),
        // !!!                          or ObjectToSTRINGREF(*(StringObject**)pObj)
        // !!! 2.  There is a real GC hole here.
        // !!! Either way you need to fix the code.
        _ASSERTE(Thread::IsObjRefValid(&objref));
        VALIDATEOBJECT(m_asObj);
        // If this assert fires, you probably did not protect
        // your OBJECTREF and a GC might have occurred.  To
        // where the possible GC was, set a breakpoint in Thread::TriggersGC
        _ASSERTE(Thread::IsObjRefValid(this));

        if (m_asObj != 0 || objref.m_asObj != 0) {
            ENABLESTRESSHEAP();
        }
    }
    return m_asObj == objref.m_asObj;
}

//-------------------------------------------------------------
// Compare two OBJECTREF's.
//-------------------------------------------------------------
int OBJECTREF::operator!=(const OBJECTREF &objref) const
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;

    if (objref.m_asObj != NULL)  // Allow comparison to zero in preemptive mode
    {
        // REVISIT_TODO: Weakening the contract system a little bit here. We should really
        // add a special NULLOBJECTREF which can be used for these situations and have
        // a separate code path for that with the correct contract protections.
        STATIC_CONTRACT_VIOLATION(ModeViolation);

        VALIDATEOBJECT(objref.m_asObj);

        // !!! If this assert is fired, there are two possibilities:
        // !!! 1.  You are doing a type cast, e.g.  *(OBJECTREF*)pObj
        // !!!     Instead, you should use ObjectToOBJECTREF(*(Object**)pObj),
        // !!!                          or ObjectToSTRINGREF(*(StringObject**)pObj)
        // !!! 2.  There is a real GC hole here.
        // !!! Either way you need to fix the code.
        _ASSERTE(Thread::IsObjRefValid(&objref));
        VALIDATEOBJECT(m_asObj);
        // If this assert fires, you probably did not protect
        // your OBJECTREF and a GC might have occurred.  To
        // where the possible GC was, set a breakpoint in Thread::TriggersGC
        _ASSERTE(Thread::IsObjRefValid(this));

        if (m_asObj != 0 || objref.m_asObj != 0) {
            ENABLESTRESSHEAP();
        }
    }

    return m_asObj != objref.m_asObj;
}


//-------------------------------------------------------------
// Forward method calls.
//-------------------------------------------------------------
Object* OBJECTREF::operator->()
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;

    VALIDATEOBJECT(m_asObj);
        // If this assert fires, you probably did not protect
        // your OBJECTREF and a GC might have occurred.  To
        // where the possible GC was, set a breakpoint in Thread::TriggersGC
    _ASSERTE(Thread::IsObjRefValid(this));

    if (m_asObj != 0) {
        ENABLESTRESSHEAP();
    }

    // if you are using OBJECTREF directly,
    // you probably want an Object *
    return (Object *)m_asObj;
}


//-------------------------------------------------------------
// Forward method calls.
//-------------------------------------------------------------
const Object* OBJECTREF::operator->() const
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;

    VALIDATEOBJECT(m_asObj);
        // If this assert fires, you probably did not protect
        // your OBJECTREF and a GC might have occurred.  To
        // where the possible GC was, set a breakpoint in Thread::TriggersGC
    _ASSERTE(Thread::IsObjRefValid(this));

    if (m_asObj != 0) {
        ENABLESTRESSHEAP();
    }

    // if you are using OBJECTREF directly,
    // you probably want an Object *
    return (Object *)m_asObj;
}


//-------------------------------------------------------------
// Assignment. We don't validate the destination so as not
// to break the sequence:
//
//      OBJECTREF or;
//      or = ...;
//-------------------------------------------------------------
OBJECTREF& OBJECTREF::operator=(const OBJECTREF &objref)
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;

    VALIDATEOBJECT(objref.m_asObj);

    // !!! If this assert is fired, there are two possibilities:
    // !!! 1.  You are doing a type cast, e.g.  *(OBJECTREF*)pObj
    // !!!     Instead, you should use ObjectToOBJECTREF(*(Object**)pObj),
    // !!!                          or ObjectToSTRINGREF(*(StringObject**)pObj)
    // !!! 2.  There is a real GC hole here.
    // !!! Either way you need to fix the code.
    _ASSERTE(Thread::IsObjRefValid(&objref));

    if ((objref.m_asObj != 0) &&
        ((IGCHeap*)GCHeapUtilities::GetGCHeap())->IsHeapPointer( (BYTE*)this ))
    {
        _ASSERTE(!"Write Barrier violation. Must use SetObjectReference() to assign OBJECTREF's into the GC heap!");
    }
    Thread::ObjectRefAssign(this);

    m_asObj = objref.m_asObj;
    if (m_asObj != 0) {
        ENABLESTRESSHEAP();
    }
    return *this;
}

//-------------------------------------------------------------
// Allows for the assignment of NULL to a OBJECTREF
//-------------------------------------------------------------

OBJECTREF& OBJECTREF::operator=(TADDR nul)
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;

    _ASSERTE(nul == 0);
    Thread::ObjectRefAssign(this);
    m_asObj = (Object*)nul;
    if (m_asObj != 0) {
        ENABLESTRESSHEAP();
    }
    return *this;
}
#endif  // DEBUG

#ifdef _DEBUG

void* __cdecl GCSafeMemCpy(void * dest, const void * src, size_t len)
{
    STATIC_CONTRACT_NOTHROW;
    STATIC_CONTRACT_GC_NOTRIGGER;
    STATIC_CONTRACT_FORBID_FAULT;

    if (!(((*(BYTE**)&dest) <  g_lowest_address ) ||
          ((*(BYTE**)&dest) >= g_highest_address)))
    {
        Thread* pThread = GetThreadNULLOk();

        // GCHeapUtilities::IsHeapPointer has race when called in preemptive mode. It walks the list of segments
        // that can be modified by GC. Do the check below only if it is safe to do so.
        if (pThread != NULL && pThread->PreemptiveGCDisabled())
        {
            // Note there is memcpyNoGCRefs which will allow you to do a memcpy into the GC
            // heap if you really know you don't need to call the write barrier

            _ASSERTE(!GCHeapUtilities::GetGCHeap()->IsHeapPointer((BYTE *) dest) ||
                     !"using memcpy to copy into the GC heap, use CopyValueClass");
        }
    }
    return memcpyNoGCRefs(dest, src, len);
}

#endif // _DEBUG

// This function clears a piece of memory in a GC safe way.  It makes the guarantee
// that it will clear memory in at least pointer sized chunks whenever possible.
// Unaligned memory at the beginning and remaining bytes at the end are written bytewise.
// We must make this guarantee whenever we clear memory in the GC heap that could contain
// object references.  The GC or other user threads can read object references at any time,
// clearing them bytewise can result in a read on another thread getting incorrect data.
void __fastcall ZeroMemoryInGCHeap(void* mem, size_t size)
{
    WRAPPER_NO_CONTRACT;
    BYTE* memBytes = (BYTE*) mem;
    BYTE* endBytes = &memBytes[size];

    // handle unaligned bytes at the beginning
    while (!IS_ALIGNED(memBytes, sizeof(PTR_PTR_VOID)) && memBytes < endBytes)
        *memBytes++ = 0;

    // now write pointer sized pieces
    // volatile ensures that this doesn't get optimized back into a memset call
    size_t nPtrs = (endBytes - memBytes) / sizeof(PTR_PTR_VOID);
    PTR_VOID volatile * memPtr = (PTR_PTR_VOID) memBytes;
    for (size_t i = 0; i < nPtrs; i++)
        *memPtr++ = 0;

    // handle remaining bytes at the end
    memBytes = (BYTE*) memPtr;
    while (memBytes < endBytes)
        *memBytes++ = 0;
}

void StackTraceArray::Append(StackTraceElement const * elem)
{
    CONTRACTL
    {
        THROWS;
        GC_TRIGGERS;
        MODE_COOPERATIVE;
        PRECONDITION(IsProtectedByGCFrame((OBJECTREF*)this));
    }
    CONTRACTL_END;

    // Only the thread that has created the array can append to it
    assert(GetObjectThread() == GetThreadNULLOk());

    uint32_t newsize = Size() + 1;
    _ASSERTE(newsize <= Capacity());
    memcpyNoGCRefs(GetData() + Size(), elem, sizeof(StackTraceElement));
    SetSize(newsize);

#if defined(_DEBUG)
    CheckState();
#endif
}

void StackTraceArray::CheckState() const
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    if (!m_array)
        return;

    _ASSERTE(GetObjectThread() == GetThreadNULLOk());

    uint32_t size = Size();
    StackTraceElement const * p;
    p = GetData();
    for (uint32_t i = 0; i < size; ++i)
        _ASSERTE(p[i].pFunc != NULL);
}

void StackTraceArray::Allocate(size_t size)
{
    CONTRACTL
    {
        THROWS;
        GC_TRIGGERS;
        MODE_COOPERATIVE;
        PRECONDITION(IsProtectedByGCFrame((OBJECTREF*)this));
    }
    CONTRACTL_END;

    S_SIZE_T raw_size = S_SIZE_T(size) * S_SIZE_T(sizeof(StackTraceElement)) + S_SIZE_T(sizeof(ArrayHeader));

    if (raw_size.IsOverflow() || !FitsIn<DWORD>(raw_size.Value()))
    {
        EX_THROW(EEMessageException, (kOverflowException, IDS_EE_ARRAY_DIMENSIONS_EXCEEDED));
    }

    SetArray(I1ARRAYREF(AllocatePrimitiveArray(ELEMENT_TYPE_I1, static_cast<DWORD>(raw_size.Value()))));
    SetSize(0);
    SetKeepAliveItemsCount(0);
    SetObjectThread();
}

size_t StackTraceArray::Capacity() const
{
    WRAPPER_NO_CONTRACT;
    if (!m_array)
    {
        return 0;
    }

    return (m_array->GetNumComponents() - sizeof(ArrayHeader)) / sizeof(StackTraceElement);
}

// Compute the number of methods in the stack trace that can be collected. We need to store keepAlive
// objects (Resolver / LoaderAllocator) for these methods.
uint32_t StackTraceArray::ComputeKeepAliveItemsCount()
{
    LIMITED_METHOD_CONTRACT;

    uint32_t count = 0;
    for (uint32_t i = 0; i < Size(); i++)
    {
        if ((*this)[i].flags & STEF_KEEPALIVE)
        {
            count++;
        }
    }

    return count;
}

uint32_t StackTraceArray::CopyDataFrom(StackTraceArray const & src)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
        PRECONDITION(IsProtectedByGCFrame((OBJECTREF*)this));
        PRECONDITION(IsProtectedByGCFrame((OBJECTREF*)&src));
    }
    CONTRACTL_END;

    uint32_t size = src.Size();
    memcpyNoGCRefs(GetRaw(), src.GetRaw(), size * sizeof(StackTraceElement) + sizeof(ArrayHeader));
    // Affinitize the copy with the current thread
    SetObjectThread();

    return size;
}

#ifdef _DEBUG
//===============================================================================
// Code that ensures that our unmanaged version of Nullable is consistant with
// the managed version Nullable<T> for all T.

void Nullable::CheckFieldOffsets(TypeHandle nullableType)
{
    LIMITED_METHOD_CONTRACT;

/***
        // The non-instantiated method tables like List<T> that are used
        // by reflection and verification do not have correct field offsets
        // but we never make instances of these anyway.
    if (nullableMT->ContainsGenericVariables())
        return;
***/

    MethodTable* nullableMT = nullableType.GetMethodTable();

        // ensure that the managed version of the table is the same as the
        // unmanaged.  Note that we can't do this in corelib.h because this
        // class is generic and field layout depends on the instantiation.

    _ASSERTE(nullableMT->GetNumInstanceFields() == 2);
    FieldDesc* field = nullableMT->GetApproxFieldDescListRaw();

    _ASSERTE(strcmp(field->GetDebugName(), "hasValue") == 0);
//     _ASSERTE(field->GetOffset() == offsetof(Nullable, hasValue));
    field++;

    _ASSERTE(strcmp(field->GetDebugName(), "value") == 0);
//     _ASSERTE(field->GetOffset() == offsetof(Nullable, value));
}
#endif

//===============================================================================
// Returns true if nullableMT is Nullable<T> for T is equivalent to paramMT

BOOL Nullable::IsNullableForTypeHelper(MethodTable* nullableMT, MethodTable* paramMT)
{
    CONTRACTL
    {
        THROWS;
        GC_TRIGGERS;
        MODE_ANY;
    }
    CONTRACTL_END;
    if (!nullableMT->IsNullable())
        return FALSE;

    // we require the parameter types to be equivalent
    return TypeHandle(paramMT).IsEquivalentTo(nullableMT->GetInstantiation()[0]);
}

//===============================================================================
int32_t Nullable::GetValueAddrOffset(MethodTable* nullableMT)
{
    LIMITED_METHOD_CONTRACT;

    _ASSERTE(IsNullableType(nullableMT));
    _ASSERTE(strcmp(nullableMT->GetApproxFieldDescListRaw()[1].GetDebugName(), "value") == 0);
    _ASSERTE(nullableMT->GetApproxFieldDescListRaw()[1].GetOffset() == nullableMT->GetNullableValueAddrOffset());
    return nullableMT->GetNullableValueAddrOffset();
}

CLR_BOOL* Nullable::HasValueAddr(MethodTable* nullableMT) {

    LIMITED_METHOD_CONTRACT;

    _ASSERTE(strcmp(nullableMT->GetApproxFieldDescListRaw()[0].GetDebugName(), "hasValue") == 0);
    _ASSERTE(nullableMT->GetApproxFieldDescListRaw()[0].GetOffset() == 0);
    return (CLR_BOOL*) this;
}

//===============================================================================
void* Nullable::ValueAddr(MethodTable* nullableMT) {

    LIMITED_METHOD_CONTRACT;

    _ASSERTE(strcmp(nullableMT->GetApproxFieldDescListRaw()[1].GetDebugName(), "value") == 0);
    _ASSERTE(nullableMT->GetApproxFieldDescListRaw()[1].GetOffset() == nullableMT->GetNullableValueAddrOffset());
    return (((BYTE*) this) + nullableMT->GetNullableValueAddrOffset());
}

//===============================================================================
// Special logic to box a nullable<T> as a boxed<T>

OBJECTREF Nullable::Box(void* srcPtr, MethodTable* nullableMT)
{
    CONTRACTL
    {
        THROWS;
        GC_TRIGGERS;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    FAULT_NOT_FATAL();      // FIX_NOW: why do we need this?

    Nullable* src = (Nullable*) srcPtr;

    _ASSERTE(IsNullableType(nullableMT));
    // We better have a concrete instantiation, or our field offset asserts are not useful
    _ASSERTE(!nullableMT->ContainsGenericVariables());

    if (!*src->HasValueAddr(nullableMT))
        return NULL;

    OBJECTREF obj = 0;
    GCPROTECT_BEGININTERIOR (src);
    MethodTable* argMT = nullableMT->GetInstantiation()[0].AsMethodTable();

    // MethodTable::Allocate() triggers cctors, so to avoid that we
    // allocate directly without triggering cctors - boxing should not trigger cctors.
    argMT->EnsureInstanceActive();
    obj = AllocateObject(argMT);

    CopyValueClass(obj->UnBox(), src->ValueAddr(nullableMT), argMT);
    GCPROTECT_END ();

    return obj;
}

//===============================================================================
// Special Logic to unbox a boxed T as a nullable<T>

BOOL Nullable::UnBox(void* destPtr, OBJECTREF boxedVal, MethodTable* destMT)
{
    CONTRACTL
    {
        THROWS;
        GC_TRIGGERS;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;
    Nullable* dest = (Nullable*) destPtr;
    BOOL fRet = TRUE;

    // We should only get here if we are unboxing a T as a Nullable<T>
    _ASSERTE(IsNullableType(destMT));

    // We better have a concrete instantiation, or our field offset asserts are not useful
    _ASSERTE(!destMT->ContainsGenericVariables());

    if (boxedVal == NULL)
    {
        // Logically we are doing *dest->HasValueAddr(destMT) = false;
        // We zero out the whole structure because it may contain GC references
        // and these need to be initialized to zero.   (could optimize in the non-GC case)
        InitValueClass(destPtr, destMT);
        fRet = TRUE;
    }
    else
    {
        GCPROTECT_BEGIN(boxedVal);
        if (!IsNullableForType(destMT, boxedVal->GetMethodTable()))
        {
            // For safety's sake, also allow true nullables to be unboxed normally.
            // This should not happen normally, but we want to be robust
            if (destMT->IsEquivalentTo(boxedVal->GetMethodTable()))
            {
                CopyValueClass(dest, boxedVal->GetData(), destMT);
                fRet = TRUE;
            }
            else
            {
                fRet = FALSE;
            }
        }
        else
        {
            *dest->HasValueAddr(destMT) = true;
            CopyValueClass(dest->ValueAddr(destMT), boxedVal->UnBox(), boxedVal->GetMethodTable());
            fRet = TRUE;
        }
        GCPROTECT_END();
    }
    return fRet;
}

//===============================================================================
// Special Logic to unbox a boxed T as a nullable<T>
// Does not do any type checks.
void Nullable::UnBoxNoCheck(void* destPtr, OBJECTREF boxedVal, MethodTable* destMT)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;
    Nullable* dest = (Nullable*) destPtr;

    // We should only get here if we are unboxing a T as a Nullable<T>
    _ASSERTE(IsNullableType(destMT));

    // We better have a concrete instantiation, or our field offset asserts are not useful
    _ASSERTE(!destMT->ContainsGenericVariables());

    if (boxedVal == NULL)
    {
        // Logically we are doing *dest->HasValueAddr(destMT) = false;
        // We zero out the whole structure because it may contain GC references
        // and these need to be initialized to zero.   (could optimize in the non-GC case)
        InitValueClass(destPtr, destMT);
    }
    else
    {
        if (IsNullableType(boxedVal->GetMethodTable()))
        {
            // For safety's sake, also allow true nullables to be unboxed normally.
            // This should not happen normally, but we want to be robust
            CopyValueClass(dest, boxedVal->GetData(), destMT);
            return;
        }

        *dest->HasValueAddr(destMT) = true;
        CopyValueClass(dest->ValueAddr(destMT), boxedVal->UnBox(), boxedVal->GetMethodTable());
    }
}

//===============================================================================
// a boxed Nullable<T> should either be null or a boxed T, but sometimes it is
// useful to have a 'true' boxed Nullable<T> (that is it has two fields).  This
// function returns a 'normalized' version of this pointer.

OBJECTREF Nullable::NormalizeBox(OBJECTREF obj) {
    CONTRACTL
    {
        THROWS;
        GC_TRIGGERS;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

    if (obj != NULL) {
        MethodTable* retMT = obj->GetMethodTable();
        if (Nullable::IsNullableType(retMT))
            obj = Nullable::Box(obj->GetData(), retMT);
    }
    return obj;
}


void ThreadBaseObject::SetInternal(Thread *it)
{
    WRAPPER_NO_CONTRACT;

    // only allow a transition from NULL to non-NULL
    _ASSERTE((m_InternalThread == NULL) && (it != NULL));
    m_InternalThread = it;

    // Now the native Thread will only be destroyed after the managed Thread is collected.
    // Tell the GC that the managed Thread actually represents much more memory.
    GCInterface::AddMemoryPressure(sizeof(Thread));
}

void ThreadBaseObject::ClearInternal()
{
    WRAPPER_NO_CONTRACT;

    _ASSERTE(m_InternalThread != NULL);
    m_InternalThread = NULL;
    GCInterface::RemoveMemoryPressure(sizeof(Thread));
}

#endif // #ifndef DACCESS_COMPILE


StackTraceElement const & StackTraceArray::operator[](size_t index) const
{
    WRAPPER_NO_CONTRACT;
    return GetData()[index];
}

StackTraceElement & StackTraceArray::operator[](size_t index)
{
    WRAPPER_NO_CONTRACT;
    return GetData()[index];
}

#if !defined(DACCESS_COMPILE)

// If there are any dynamic methods, the stack trace object is the dynamic methods array with its first
// slot set to the stack trace I1Array.
// Otherwise the stack trace object is directly the stacktrace I1Array
void ExceptionObject::SetStackTrace(OBJECTREF stackTrace)
{
    CONTRACTL
    {
        GC_NOTRIGGER;
        NOTHROW;
        MODE_COOPERATIVE;
    }
    CONTRACTL_END;

#ifdef STRESS_LOG
    if (StressLog::StressLogOn(~0u, 0))
    {
        StressLog::CreateThreadStressLog();
    }
#endif

    SetObjectReference((OBJECTREF*)&_stackTrace, (OBJECTREF)stackTrace);
}
#endif // !defined(DACCESS_COMPILE)

// Get the stack trace and keep alive array for the exception.
// Both arrays returned by the method are safe to work with without other threads modifying them.
// - if the stack trace was created by the current thread, the arrays are returned as is.
// - if it was created by another thread, deep copies of the arrays are returned. It is ensured
//   that both of these arrays are consistent. That means that the stack trace doesn't contain
//   frames that need keep alive objects and that are not protected by entries in the keep alive
//   array.
void ExceptionObject::GetStackTrace(StackTraceArray & stackTrace, PTRARRAYREF * outKeepAliveArray, Thread *pCurrentThread) const
{
    CONTRACTL
    {
        GC_TRIGGERS;
        THROWS;
        MODE_COOPERATIVE;
        PRECONDITION(IsProtectedByGCFrame((OBJECTREF*)&stackTrace));
        PRECONDITION(IsProtectedByGCFrame((OBJECTREF*)outKeepAliveArray));
    }
    CONTRACTL_END;

    ExceptionObject::GetStackTraceParts(_stackTrace, stackTrace, outKeepAliveArray);

#ifndef DACCESS_COMPILE
    if ((stackTrace.Get() != NULL) && (stackTrace.GetObjectThread() != pCurrentThread))
    {
        GetStackTraceClone(stackTrace, outKeepAliveArray);
    }
#endif // DACCESS_COMPILE
}

#ifndef DACCESS_COMPILE
void ExceptionObject::GetStackTraceClone(StackTraceArray & stackTrace, PTRARRAYREF * outKeepAliveArray)
{
    {
        struct
        {
            StackTraceArray newStackTrace;
            PTRARRAYREF newKeepAliveArray = NULL;
        } gc;

        GCPROTECT_BEGIN(gc);

        // When the stack trace was created by other thread than the current one, we create a copy of both the stack trace and the keepAlive arrays to make sure
        // they are not changing while the caller is accessing them.
        gc.newStackTrace.Allocate(stackTrace.Capacity());
        uint32_t numCopiedFrames = gc.newStackTrace.CopyDataFrom(stackTrace);
        // Set size to the exact value which was used when we copied the data,
        // another thread might have changed it at the time of copying
        gc.newStackTrace.SetSize(numCopiedFrames);

        stackTrace.Set(gc.newStackTrace.Get());

        uint32_t keepAliveArrayCapacity = ((*outKeepAliveArray) == NULL) ? 0 : (*outKeepAliveArray)->GetNumComponents();

        // It is possible that another thread was modifying the stack trace array and keep alive array while we were making the copies.
        // The following sequence of events could have happened:
        // Case 1:
        // * The current thread gets the stack trace array and the keep alive array references using the ExceptionObject::GetStackTraceParts above
        // * The current thread reads the size of the stack trace array
        // * Another thread adds a new stack frame to the stack trace array and a corresponding keep alive object to the keep alive array
        // * The current thread creates a copy of the stack trace using the size it read before
        // * The keep alive count stored in the stack trace array doesn't match the elements in the copy of the stack trace array
        // In this case, we need to recompute the keep alive count based on the copied stack trace array.
        //
        // Case 2:
        // * The current thread gets the stack trace array and the keep alive array references using the ExceptionObject::GetStackTraceParts above
        // * Another thread adds a stack frame with a keep alive item and that exceeds the keep alive array capacity. So it allocates a new keep alive array
        //   and adds the new keep alive item to it.
        // * Thus the keep alive array this thread has read doesn't have the keep alive item, but the stack trace array contains the element the other thread has added.
        // In this case, we need to trim the stack trace array at the first element that doesn't have a corresponding keep alive object in the keep alive array.
        // We cannot fetch the keep alive object for that stack trace entry, because in the meanwhile, the keep alive array that the other thread created
        // may have been collected and the method related to the stack trace entry may have been collected as well.
        //
        uint32_t keepAliveItemsCount = 0;
        for (uint32_t i = 0; i < numCopiedFrames; i++)
        {
            if (stackTrace[i].flags & STEF_KEEPALIVE)
            {
                if ((keepAliveItemsCount + 1) >= keepAliveArrayCapacity)
                {
                    // Trim the stack trace at a point where a dynamic or collectible method is found without a corresponding keepAlive object.
                    stackTrace.SetSize(i);
                    break;
                }
                else
                {
                    _ASSERTE((*outKeepAliveArray)->GetAt(keepAliveItemsCount + 1) != NULL);
                }

                keepAliveItemsCount++;
            }
        }

        stackTrace.SetKeepAliveItemsCount(keepAliveItemsCount);
        _ASSERTE(stackTrace.ComputeKeepAliveItemsCount() == keepAliveItemsCount);

        if (keepAliveArrayCapacity != 0)
        {
            gc.newKeepAliveArray = (PTRARRAYREF)AllocateObjectArray(keepAliveArrayCapacity, g_pObjectClass);
            _ASSERTE((*outKeepAliveArray) != NULL);
            memmoveGCRefs(gc.newKeepAliveArray->GetDataPtr() + 1,
                          (*outKeepAliveArray)->GetDataPtr() + 1,
                          (keepAliveItemsCount) * sizeof(Object *));
            gc.newKeepAliveArray->SetAt(0, stackTrace.Get());
            *outKeepAliveArray = gc.newKeepAliveArray;
        }
        else
        {
            *outKeepAliveArray = NULL;
        }
        GCPROTECT_END();
    }
}
#endif // DACCESS_COMPILE

// Get the stack trace and the dynamic method array from the stack trace object.
// If the stack trace was created by another thread, it returns clones of both arrays.
/* static */
void ExceptionObject::GetStackTraceParts(OBJECTREF stackTraceObj, StackTraceArray & stackTrace, PTRARRAYREF * outKeepAliveArray /*= NULL*/)
{
    CONTRACTL
    {
        GC_TRIGGERS;
        THROWS;
        MODE_COOPERATIVE;
        PRECONDITION(IsProtectedByGCFrame((OBJECTREF*)&stackTrace));
        PRECONDITION(IsProtectedByGCFrame((OBJECTREF*)outKeepAliveArray));
    }
    CONTRACTL_END;

    PTRARRAYREF keepAliveArray = NULL;

    // Extract the stack trace and keepAlive arrays from the stack trace object.
    if ((stackTraceObj != NULL) && ((dac_cast<PTR_ArrayBase>(OBJECTREFToObject(stackTraceObj)))->GetMethodTable()->ContainsGCPointers()))
    {
        // The stack trace object is the dynamic methods array with its first slot set to the stack trace I1Array.
        PTR_PTRArray combinedArray = dac_cast<PTR_PTRArray>(OBJECTREFToObject(stackTraceObj));
        stackTrace.Set(dac_cast<I1ARRAYREF>(combinedArray->GetAt(0)));
        keepAliveArray = dac_cast<PTRARRAYREF>(ObjectToOBJECTREF(combinedArray));
    }
    else
    {
        stackTrace.Set(dac_cast<I1ARRAYREF>(stackTraceObj));
    }

    if (outKeepAliveArray != NULL)
    {
        *outKeepAliveArray = keepAliveArray;
    }
}

#ifndef DACCESS_COMPILE
void LoaderAllocatorObject::SetSlotsUsed(INT32 newSlotsUsed)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
        PRECONDITION(m_pLoaderAllocatorScout->m_nativeLoaderAllocator->HasHandleTableLock());
    }
    CONTRACTL_END;

    m_slotsUsed = newSlotsUsed;
}

PTRARRAYREF LoaderAllocatorObject::GetHandleTable()
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
        PRECONDITION(m_pLoaderAllocatorScout->m_nativeLoaderAllocator->HasHandleTableLock());
    }
    CONTRACTL_END;

    return (PTRARRAYREF)m_pSlots;
}

void LoaderAllocatorObject::SetHandleTable(PTRARRAYREF handleTable)
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
        PRECONDITION(m_pLoaderAllocatorScout->m_nativeLoaderAllocator->HasHandleTableLock());
    }
    CONTRACTL_END;

    SetObjectReference(&m_pSlots, (OBJECTREF)handleTable);
}

INT32 LoaderAllocatorObject::GetSlotsUsed()
{
    CONTRACTL
    {
        NOTHROW;
        GC_NOTRIGGER;
        MODE_COOPERATIVE;
        PRECONDITION(m_pLoaderAllocatorScout->m_nativeLoaderAllocator->HasHandleTableLock());
    }
    CONTRACTL_END;

    return m_slotsUsed;
}

#ifdef DEBUG
static void CheckOffsetOfFieldInInstantiation(MethodTable *pMTOfInstantiation, FieldDesc* pField, size_t offset)
{
    STANDARD_VM_CONTRACT;

    MethodTable* pGenericFieldMT = pField->GetApproxEnclosingMethodTable();
    DWORD index = pGenericFieldMT->GetIndexForFieldDesc(pField);
    FieldDesc *pFieldOnInstantiation = pMTOfInstantiation->GetFieldDescByIndex(index);

    if (pFieldOnInstantiation->GetOffset() != offset)
    {
        _ASSERTE(!"Field offset mismatch");
    }
}

/*static*/ void GenericCacheStruct::ValidateLayout(MethodTable* pMTOfInstantiation)
{
    STANDARD_VM_CONTRACT;

    CheckOffsetOfFieldInInstantiation(pMTOfInstantiation, CoreLibBinder::GetField(FIELD__GENERICCACHE__TABLE), offsetof(GenericCacheStruct, _table));
    CheckOffsetOfFieldInInstantiation(pMTOfInstantiation, CoreLibBinder::GetField(FIELD__GENERICCACHE__SENTINEL_TABLE), offsetof(GenericCacheStruct, _sentinelTable));
    CheckOffsetOfFieldInInstantiation(pMTOfInstantiation, CoreLibBinder::GetField(FIELD__GENERICCACHE__LAST_FLUSH_SIZE), offsetof(GenericCacheStruct, _lastFlushSize));
    CheckOffsetOfFieldInInstantiation(pMTOfInstantiation, CoreLibBinder::GetField(FIELD__GENERICCACHE__INITIAL_CACHE_SIZE), offsetof(GenericCacheStruct, _initialCacheSize));
    CheckOffsetOfFieldInInstantiation(pMTOfInstantiation, CoreLibBinder::GetField(FIELD__GENERICCACHE__MAX_CACHE_SIZE), offsetof(GenericCacheStruct, _maxCacheSize));
    // Validate the layout of the Generic
}
#endif

#endif // DACCESS_COMPILE
