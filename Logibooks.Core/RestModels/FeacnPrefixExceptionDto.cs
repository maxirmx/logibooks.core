// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Models;

namespace Logibooks.Core.RestModels;

public class FeacnPrefixExceptionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int FeacnPrefixId { get; set; }
    public FeacnPrefixExceptionDto(FeacnPrefixException e)
    {
        Id = e.Id;
        Code = e.Code;
        FeacnPrefixId = e.FeacnPrefixId;
    }
}
