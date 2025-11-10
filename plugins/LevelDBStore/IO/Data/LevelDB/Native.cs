// Copyright (C) 2015-2025 The Neo Project.
//
// Native.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Neo.IO.Data.LevelDB;

public enum CompressionType : byte
{
    NoCompression = 0x0,
    SnappyCompression = 0x1
}

internal static partial class Native
{
    #region Logger

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial nint leveldb_logger_create(nint /* Action<string> */ logger);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_logger_destroy(nint /* logger*/ option);

    #endregion

    #region DB

    [LibraryImport("libleveldb", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial nint leveldb_open(nint /* Options*/ options, string name, out nint error);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_close(nint /*DB */ db);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_put(nint /* DB */ db, nint /* WriteOptions*/ options,
        [In] byte[] key, nuint keylen, [In] byte[] val, nuint vallen, out nint errptr);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_delete(nint /* DB */ db, nint /* WriteOptions*/ options,
        [In] byte[] key, nuint keylen, out nint errptr);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_write(nint /* DB */ db, nint /* WriteOptions*/ options, nint /* WriteBatch */ batch, out nint errptr);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial nint leveldb_get(nint /* DB */ db, nint /* ReadOptions*/ options,
        [In] byte[] key, nuint keylen, out nuint vallen, out nint errptr);

    // [DllImport("libleveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    // static extern void leveldb_approximate_sizes(nint /* DB */ db, int num_ranges,
    // byte[] range_start_key, long range_start_key_len, byte[] range_limit_key, long range_limit_key_len, out long sizes);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial nint leveldb_create_iterator(nint /* DB */ db, nint /* ReadOption */ options);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial nint leveldb_create_snapshot(nint /* DB */ db);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_release_snapshot(nint /* DB */ db, nint /* SnapShot*/ snapshot);

    [LibraryImport("libleveldb", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial nint leveldb_property_value(nint /* DB */ db, string propname);

    [LibraryImport("libleveldb", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_repair_db(nint /* Options*/ options, string name, out nint error);

    [LibraryImport("libleveldb", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(AnsiStringMarshaller))]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_destroy_db(nint /* Options*/ options, string name, out nint error);

    #region extensions

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_free(nint /* void */ ptr);

    #endregion
    #endregion

    #region Env

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial nint leveldb_create_default_env();

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_env_destroy(nint /*Env*/ cache);

    #endregion

    #region Iterator

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_iter_destroy(nint /*Iterator*/ iterator);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool leveldb_iter_valid(nint /*Iterator*/ iterator);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_iter_seek_to_first(nint /*Iterator*/ iterator);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_iter_seek_to_last(nint /*Iterator*/ iterator);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_iter_seek(nint /*Iterator*/ iterator, [In] byte[] key, nuint length);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_iter_next(nint /*Iterator*/ iterator);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_iter_prev(nint /*Iterator*/ iterator);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial nint leveldb_iter_key(nint /*Iterator*/ iterator, out nuint length);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial nint leveldb_iter_value(nint /*Iterator*/ iterator, out nuint length);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_iter_get_error(nint /*Iterator*/ iterator, out nint error);

    #endregion

    #region Options

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial nint leveldb_options_create();

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_options_destroy(nint /*Options*/ options);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_options_set_create_if_missing(nint /*Options*/ options, [MarshalAs(UnmanagedType.U1)] bool o);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_options_set_error_if_exists(nint /*Options*/ options, [MarshalAs(UnmanagedType.U1)] bool o);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_options_set_info_log(nint /*Options*/ options, nint /* Logger */ logger);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_options_set_paranoid_checks(nint /*Options*/ options, [MarshalAs(UnmanagedType.U1)] bool o);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_options_set_env(nint /*Options*/ options, nint /*Env*/ env);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_options_set_write_buffer_size(nint /*Options*/ options, nuint size);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_options_set_max_open_files(nint /*Options*/ options, int max);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_options_set_cache(nint /*Options*/ options, nint /*Cache*/ cache);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_options_set_block_size(nint /*Options*/ options, nuint size);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_options_set_block_restart_interval(nint /*Options*/ options, int interval);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_options_set_compression(nint /*Options*/ options, CompressionType level);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_options_set_comparator(nint /*Options*/ options, nint /*Comparator*/ comparer);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_options_set_filter_policy(nint /*Options*/ options, nint /*FilterPolicy*/ policy);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial nint leveldb_filterpolicy_create_bloom(int bits_per_key);

    #endregion

    #region ReadOptions

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial nint leveldb_readoptions_create();

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_readoptions_destroy(nint /*ReadOptions*/ options);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_readoptions_set_verify_checksums(nint /*ReadOptions*/ options, [MarshalAs(UnmanagedType.U1)] bool o);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_readoptions_set_fill_cache(nint /*ReadOptions*/ options, [MarshalAs(UnmanagedType.U1)] bool o);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_readoptions_set_snapshot(nint /*ReadOptions*/ options, nint /*SnapShot*/ snapshot);

    #endregion

    #region WriteBatch

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial nint leveldb_writebatch_create();

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_writebatch_destroy(nint /* WriteBatch */ batch);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_writebatch_clear(nint /* WriteBatch */ batch);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_writebatch_put(nint /* WriteBatch */ batch, [In] byte[] key, nuint keylen, [In] byte[] val, nuint vallen);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_writebatch_delete(nint /* WriteBatch */ batch, [In] byte[] key, nuint keylen);

    #endregion

    #region WriteOptions

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial nint leveldb_writeoptions_create();

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_writeoptions_destroy(nint /*WriteOptions*/ options);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_writeoptions_set_sync(nint /*WriteOptions*/ options, [MarshalAs(UnmanagedType.U1)] bool o);

    #endregion

    #region Cache

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial nint leveldb_cache_create_lru(int capacity);

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_cache_destroy(nint /*Cache*/ cache);

    #endregion

    #region Comparator

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial nint /* leveldb_comparator_t* */ leveldb_comparator_create(
        nint state, // void* state
        nint destructor, // void (*destructor)(void*)
        nint compare, // int (*compare)(void*, const char* a, size_t alen,const char* b, size_t blen)
        nint name); // const char* (*name)(void*)

    [LibraryImport("libleveldb")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvCdecl) })]
    internal static partial void leveldb_comparator_destroy(nint /* leveldb_comparator_t* */ cmp);

    #endregion
}

internal static class NativeHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CheckError(nint error)
    {
        if (error != nint.Zero)
        {
            var message = Marshal.PtrToStringAnsi(error);
            Native.leveldb_free(error);
            throw new LevelDBException(message ?? string.Empty);
        }
    }
}
