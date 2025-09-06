// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class TransportationTypeDto
{
    public int Id { get; set; }
    public TransportationTypeCode Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Document { get; set; } = string.Empty;

    public TransportationTypeDto(TransportationType type)
    {
        Id = type.Id;
        Code = type.Code;
        Name = type.Name;
        Document = type.Document;
    }

}
