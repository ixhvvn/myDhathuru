using System;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;
var svg = File.ReadAllText(@"backend/src/MyDhathuru.Api/wwwroot/logo-name.svg");
var bytes = Document.Create(container =>
{
    container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Content().Padding(20).Svg(svg);
    });
}).GeneratePdf();
Directory.CreateDirectory(@"output");
File.WriteAllBytes(@"output/pdf-probe-logo-name.pdf", bytes);
Console.WriteLine($"Generated {bytes.Length} bytes");
