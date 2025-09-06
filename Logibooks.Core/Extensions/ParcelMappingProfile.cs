// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using AutoMapper;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Extensions;

public class ParcelMappingProfile : Profile
{
    public ParcelMappingProfile()
    {
        MapOrderUpdate<WbrParcel>();
        MapOrderUpdate<OzonParcel>();
    }

    private void MapOrderUpdate<TOrder>() where TOrder : BaseParcel
    {
        CreateMap<ParcelUpdateItem, TOrder>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.RegisterId, opt => opt.Ignore())
            .ForMember(dest => dest.Register, opt => opt.Ignore())
            .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.ProductName))
            .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));
    }
}

