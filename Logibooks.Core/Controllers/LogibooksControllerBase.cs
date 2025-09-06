// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

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
    protected ObjectResult _400MustBe10Digits(string code)
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage() { Msg = $"Код ТН ВЭД должен состоять из 10 цифр [код={code}]" });
    }

    protected ObjectResult _400CompanyId(int companyId)
    {
               return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage() { Msg = $"Неизвестный идентификатор компании [id={companyId}]" });
    }

    protected ObjectResult _400CompanyId()
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage() { Msg = "Не указан идентификатор компании" });
    }

    protected ObjectResult _404CompanyId(int companyId)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage() { Msg = $"Неизвестный идентификатор компании [id={companyId}]" });
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

    protected ObjectResult _400KeyWordFile(string msg)
    {
        return StatusCode(StatusCodes.Status400BadRequest,
                          new ErrMessage() { Msg = msg });
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
    protected ObjectResult _404Parcel(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Не удалось найти информацию о посылке [id={id}]" });
    }
    protected ObjectResult _404ParcelNumber(string number)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Не удалось найти информацию о посылке [номер={number}]" });
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
    protected ObjectResult _409StopWord(string word)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Стоп-слово уже существует [слово = {word}]" });
    }
    protected ObjectResult _409KeyWord(string word)
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = $"Ключевое слово уже существует [слово = {word}]" });
    }

    protected ObjectResult _409Validation()
    {
        return StatusCode(StatusCodes.Status409Conflict,
                          new ErrMessage { Msg = "Проверка реестра уже выполняется" });
    }

    /// <summary>
    /// Returns a 409 Conflict response for duplicate FeacnPrefix code
    /// </summary>
    /// <param name="code">The duplicate code</param>
    /// <returns>ObjectResult with 409 status</returns>
    protected ObjectResult _409DuplicateFeacnPrefixCode(string code)
    {
        return StatusCode(StatusCodes.Status409Conflict,
            new ErrMessage { Msg = $"Код префикса '{code}' уже существует" });
    }
    protected ObjectResult _500Mapping(string fname)
    {
        return StatusCode(StatusCodes.Status500InternalServerError,
                          new ErrMessage { Msg = $"Не найдена спецификация файла реестра [имя файла = {fname}]" });
    }
    internal static ObjectResult _500(string controller, string action)
    {
        string msg = $"Запрос не выполнен из-за внутренней ошибки сервиса [{controller}: {action}]. Если ошибка будет повторяться, обратитесь к администратору системы";
        return new ObjectResult(new ErrMessage { Msg = msg })
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
    }
    protected ObjectResult _404Handle(Guid handleId)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Не удалось найти операцию проверки [id={handleId}]" });
    }

    protected ObjectResult _404FeacnCode(string code)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Не удалось найти код ТН ВЭД [код={code}]" });
    }

    protected ObjectResult _404FeacnOrder(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Не удалось найти постановление (приказ, решение) [id={id}]" });
    }

    protected ObjectResult _404FeacnPrefix(int id)
    {
        return StatusCode(StatusCodes.Status404NotFound,
                          new ErrMessage { Msg = $"Не удалось найти префикс [id={id}]" });
    }

    protected ObjectResult _403FeacnPrefix(int id)
    {
        return StatusCode(StatusCodes.Status403Forbidden,
                          new ErrMessage { Msg = $"Невозможно выполнить операцию с префиксом [id={id}], связанным с приказом" });
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
