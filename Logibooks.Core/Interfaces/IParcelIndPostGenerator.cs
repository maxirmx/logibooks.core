// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.Interfaces;

public interface IParcelIndPostGenerator
{
    Task<(string, string)> GenerateXML(int orderId);
    Task<(string, byte[])> GenerateXML4R(int registerId);
}
