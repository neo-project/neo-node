// Copyright (C) 2015-2026 The Neo Project.
//
// Helper.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Plugins.Storage;

public static class Helper
{
    public static int CompareLex(byte[] a, byte[] b)
    {
        int minLength = Math.Min(a.Length, b.Length);
        for (var i = 0; i < minLength; i++)
        {
            var diff = a[i].CompareTo(b[i]);
            if (diff != 0) return diff;
        }
        return a.Length.CompareTo(b.Length);
    }
}
