// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Extensions;

public static class RegisterExtensions
{
    public static void ApplyUpdateFrom(this Register register, RegisterUpdateItem update)
    {
        if (update == null) return;
        if (update.InvoiceNumber != null) register.InvoiceNumber = update.InvoiceNumber;
        if (update.InvoiceDate != null) 
        {
            if (string.IsNullOrWhiteSpace(update.InvoiceDate))
            {
                register.InvoiceDate = null;
            }
            else if (DateOnly.TryParse(update.InvoiceDate, out DateOnly parsedDate))
            {
                register.InvoiceDate = parsedDate;
            }
            else
            {
                register.InvoiceDate = null;
            }
        }
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

    public static RegisterViewItem ToViewItem(this Register register, Dictionary<int, int> parcelsByCheckStatus, int placesTotal)
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
            ParcelsTotal = parcelsByCheckStatus?.Values.Sum() ?? 0,
            PlacesTotal = placesTotal,
            ParcelsByCheckStatus = parcelsByCheckStatus ?? []
        };
    }
}
