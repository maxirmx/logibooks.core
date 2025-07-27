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

using System.Text;
using System.Xml.Linq;

namespace Logibooks.Core.Services
{
    public class IndPostXmlService : IIndPostXmlService
    {
        public string CreateXml(IDictionary<string, string?> fields, IEnumerable<IDictionary<string, string?>> goodsItems)
        {
            var timeValue = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
            var root = new XElement("AltaIndPost", new XAttribute("time", timeValue));

            foreach (var pair in fields)
            {
                if (pair.Value != null)
                {
                    root.Add(new XElement(pair.Key, pair.Value));
                }
            }

            foreach (var item in goodsItems)
            {
                var goods = new XElement("GOODS");
                foreach (var innerPair in item)
                {
                    if (innerPair.Value != null)
                    {
                        goods.Add(new XElement(innerPair.Key, innerPair.Value));
                    }
                }
                root.Add(goods);
            }

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
            var settings = new System.Xml.XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
                OmitXmlDeclaration = false
            };
            using var sw = new StringWriter();
            using (var xw = System.Xml.XmlWriter.Create(sw, settings))
            {
                doc.Save(xw);
            }
            return sw.ToString();
        }
    }
}
