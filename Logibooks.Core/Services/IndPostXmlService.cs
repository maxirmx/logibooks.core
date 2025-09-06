// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Interfaces;
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

            var doc = new XDocument(new XDeclaration("1.0", "utf-8  ", null), root);
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
