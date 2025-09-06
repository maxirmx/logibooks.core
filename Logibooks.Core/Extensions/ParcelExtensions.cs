// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using AutoMapper;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Extensions;

public static class ParcelExtensions
{
    public static void UpdateFrom(this WbrParcel order, ParcelUpdateItem updateItem, IMapper mapper)
    {
        mapper.Map(updateItem, order);
    }

    public static void UpdateFrom(this OzonParcel order, ParcelUpdateItem updateItem, IMapper mapper)
    {
        mapper.Map(updateItem, order);
    }
}
