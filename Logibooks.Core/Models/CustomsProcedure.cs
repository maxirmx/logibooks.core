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

using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("customs_procedures")]
public class CustomsProcedure
{
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    public short Code { get; set; }

    [Column("name")]
    public required string Name { get; set; }
}

/* 
 * 10 Экспорт
 * 12 Вывоз товаров с таможенной территории Российской Федерации, предназначенных для обеспечения функционирования посольств, 
 *    консульств, представительств при международных организациях и иных официальных представительств Российской Федерации за рубежом
 * 13 Вывоз товаров в государства - бывшие республики СССР, предназначенных для обеспечения деятельности расположенных на территориях
 *    этих государств лечебных, спортивно-оздоровительных и иных учреждений социальной сферы, имущество которых находится в 
 *    собственности Российской Федерации или субъектов Российской Федерации, а также для проведения на территориях указанных 
 *    государств российскими организациями научно-исследовательских работ в интересах Российской Федерации на некоммерческой основе
 * 21 Переработка вне таможенной территории
 * 23 Временный вывоз товаров
 * 28 Временный вывоз транспортных средств
 * 31 Реэкспорт
 * 40 Выпуск для внутреннего потребления
 * 52 Переработка на таможенной территории
 * 53 Временный ввоз товаров
 * 58 Временный ввоз транспортных средств
 * 61 Реимпорт с одновременным выпуском для свободного обращения с уплатой сумм ввозных таможенных пошлин, налогов, субсидий и иных 
 *    сумм, подлежащих возвращению в федеральный бюджет при реимпорте товаров
 * 63 Реимпорт с одновременным выпуском для свободного обращения без уплаты сумм ввозных таможенных пошлин, налогов, субсидий и иных
 *    сумм, подлежащих возвращению в федеральный бюджет при реимпорте товаров
 * 71 Таможенный склад для иностранных товаров
 * 72 Таможенный склад для российских товаров
 * 78 Свободная таможенная зона
 * 79 Свободный склад
 * 80 Международный таможенный транзит
 * 81 Перемещение российских товаров между таможенными органами через территорию иностранного государства
 * 91 Переработка для внутреннего потребления
 * 93 Уничтожение
 * 95 Припасы
 * 96 Беспошлинная торговля
 * 97 Отказ в пользу государства
 * 98 Перемещение товаров через таможенную границу между воинскими частями Российской Федерации, дислоцированными на таможенной 
 *    территории Российской Федерации и за пределами этой территории
 * 99 Перемещение товаров через таможенную границу, предназначенных для предупреждения и ликвидации стихийных бедствий и иных чрезвычайных
 * ситуаций, в том числе товаров, предназначенных для бесплатной раздачи лицам, пострадавшим в результате чрезвычайных ситуаций, и товаров, необходимых для проведения аварийно-спасательных и других неотложных работ и жизнедеятельности аварийно-спасательных формирований
 * */
