// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// 'AS IS' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Extensions;

public static class RegisterExtensions
{
    public static void ApplyUpdateFrom(this Register register, RegisterUpdateItem update)
    {
        if (update == null) return;
        if (update.InvoiceNumber != null) register.InvoiceNumber = update.InvoiceNumber;
        if (update.InvoiceDate != null) register.InvoiceDate = update.InvoiceDate;
        if (update.TheOtherCountryCode != null)
        {
            register.TheOtherCountryCode = update.TheOtherCountryCode != 0 ? update.TheOtherCountryCode : null;
        }
        if (update.TransportationTypeId != null) register.TransportationTypeId = update.TransportationTypeId.Value;
        if (update.CustomsProcedureId != null) register.CustomsProcedureId = update.CustomsProcedureId.Value;
        if (update.DealNumber != null) register.DealNumber = update.DealNumber;
        if (update.TheOtherCompanyId != null)
        {
            register.TheOtherCompanyId = update.TheOtherCompanyId != 0 ? update.TheOtherCompanyId : null;
        }

    }

    public static RegisterViewItem ToViewItem(this Register register, Dictionary<int, int> ordersByCheckStatus)
    {
        return new RegisterViewItem
        {
            Id = register.Id,
            FileName = register.FileName,
            Date = register.DTime,
            CompanyId = register.CompanyId,
            TheOtherCompanyId = register.TheOtherCompanyId ?? 0, 
            DealNumber = register.DealNumber,
            InvoiceNumber = register.InvoiceNumber,
            InvoiceDate = register.InvoiceDate,
            TheOtherCountryCode = register.TheOtherCountryCode ?? 0,
            TransportationTypeId = register.TransportationTypeId,
            CustomsProcedureId = register.CustomsProcedureId,
            OrdersTotal = ordersByCheckStatus?.Values.Sum() ?? 0,
            OrdersByCheckStatus = ordersByCheckStatus ?? new Dictionary<int, int>()
        };
    }
}
