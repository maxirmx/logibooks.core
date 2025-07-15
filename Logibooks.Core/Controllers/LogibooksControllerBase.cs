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
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using Microsoft.AspNetCore.Mvc;
using Logibooks.Core.RestModels;
using Logibooks.Core.Data;

namespace Logibooks.Core.Controllers;
public class LogibooksControllerPreBase(AppDbContext db, ILogger logger) : ControllerBase
{
    protected readonly AppDbContext _db = db;
    protected readonly ILogger _logger = logger;

    protected ObjectResult _400()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage() { Msg = "Нарушена целостность запроса" });
    }
    protected ObjectResult _400EmptyRegister()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage() { Msg = "Пустой файл реестра" });
    }
    protected ObjectResult _400NoRegister()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage() { Msg = "Файл реестра не найден в архиве" });
    }
    protected ObjectResult _400UnsupportedFileType(string ext)
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage() { Msg = $"Файлы формата {ext} не поддерживаются. Можно загрузить .xlsx, .xls, .zip, .rar" });
    }

    protected ObjectResult _401()
    {
        return StatusCode(StatusCodes.Status401Unauthorized,
                          new ErrMessage { Msg = "Неправильный адрес электронной почты или пароль" });
    }
    protected ObjectResult _403()
    {
        return StatusCode(StatusCodes.Status403Forbidden,
                          new ErrMessage { Msg = "Недостаточно прав для выполнения операции" });
    }
    protected ObjectResult _404User(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Не удалось найти пользователя [id={id}]" });
    }
    protected ObjectResult _404Register(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Не удалось найти реестр [id={id}]" });
    }
    protected ObjectResult _404Object(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Не удалось найти объект [id={id}]" });
    }
    protected ObjectResult _404Order(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Не удалось найти заказ [id={id}]" });
    }
    protected ObjectResult _404OrderNumber(string number)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Не удалось найти заказ [номер={number}]" });
    }
    protected ObjectResult _404Status(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Не удалось найти статус [id={id}]" });
    }
    protected ObjectResult _409Email(string email)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Пользователь с таким адресом электронной почты уже зарегистрирован [email = {email}]" });
    }
    protected ObjectResult _409OrderStatus()
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Невозможно удалить статус, на который ссылаются заказы" });
    }
    protected ObjectResult _409CompanyInn(string inn)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Компания с таким ИНН уже существует [ИНН = {inn}]" });
    }
    protected ObjectResult _409Company()
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Невозможно удалить компанию, на которую ссылаются загруженные реестры" });
    }
    protected ObjectResult _409Register()
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Невозможно удалить реестр, на который ссылаются заказы" });
    }

    protected ObjectResult _500Mapping(string fname)
    {
        return StatusCode(StatusCodes.Status500InternalServerError,
                          new ErrMessage { Msg = $"Не найдена спецификация файла реестра [имя файла = {fname}]" });
    }

    protected ObjectResult _500UploadRegister()
    {
        return StatusCode(StatusCodes.Status500InternalServerError,
                          new ErrMessage { Msg = "Внутренняя ошибка при загрузке файла реестра" });
    }
    protected ObjectResult _500UploadCountryCodes()
    {
        return StatusCode(StatusCodes.Status500InternalServerError,
                          new ErrMessage { Msg = "Ошибка при загрузке списка стран" });
    }

}

public class LogibooksControllerBase : LogibooksControllerPreBase
{

    protected readonly int _curUserId;

    protected LogibooksControllerBase(IHttpContextAccessor httpContextAccessor, AppDbContext db, ILogger logger): base(db, logger)
    {
        _curUserId = 0;
        var htc = httpContextAccessor.HttpContext;
        if (htc != null)
        {
            var uid = htc.Items["UserId"];
            if (uid != null) _curUserId = (int)uid;
        }
    }
}
