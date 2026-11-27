// Copyright (C) 2015-2026 The Neo Project.
//
// EnumerationPage.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Neo.Plugins.RestServer.Models.Error;
using Newtonsoft.Json;
using System.Text;

namespace Neo.Plugins.RestServer.Helpers;

internal static class EnumerationPage
{
    public static int GetOffset(int page, int size, int? maxPageSize = null)
    {
        var settings = RestServerSettings.Current;
        var end = ((long)page - 1) * size + size;
        if (page < 1 || size < 1 || size > settings.MaxPageSize || size > (maxPageSize ?? int.MaxValue) || end > settings.MaxEnumerationItems)
            throw new Exceptions.InvalidParameterRangeException();

        return (int)(end - size);
    }

    public static IActionResult BoundedResult<T>(IEnumerable<T> items)
    {
        try
        {
            using var stream = new LimitedMemoryStream(RestServerSettings.Current.MaxEnumerationResponseBytes);
            using var textWriter = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            using var jsonWriter = new JsonTextWriter(textWriter) { CloseOutput = false };
            JsonSerializer.Create(RestServerSettings.Current.JsonSerializerSettings).Serialize(jsonWriter, items);
            jsonWriter.Flush();
            return new FileContentResult(stream.ToArray(), "application/json");
        }
        catch (ResponseSizeLimitExceededException)
        {
            return new ObjectResult(new ErrorModel { Name = "ResponseTooLarge", Message = "The response exceeds MaxEnumerationResponseBytes." })
            {
                StatusCode = StatusCodes.Status413PayloadTooLarge
            };
        }
    }

    private sealed class ResponseSizeLimitExceededException : IOException { }

    private sealed class LimitedMemoryStream(int limit) : MemoryStream
    {
        private void Check(int count)
        {
            if (Length + count > limit)
                throw new ResponseSizeLimitExceededException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Check(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Check(buffer.Length);
            base.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            Check(1);
            base.WriteByte(value);
        }
    }
}
