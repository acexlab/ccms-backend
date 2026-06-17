using CCMS.Application.Interfaces;
using System;
using System.IO;
using System.Collections.Generic;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Snippets.Font;
using CCMS.Application.DTOs;

namespace CCMS.Infrastructure.Services;

public class PdfGenerator : IPdfGenerator
{
    public PdfGenerator()
    {
        try
        {
            GlobalFontSettings.FontResolver ??= new FailsafeFontResolver();
        }
        catch { }
    }

    public byte[] GenerateCourtOrder(CreateCaseDto dto, string caseNumber)
    {
        var document = new PdfDocument();
        document.Info.Title = $"Court Order - {caseNumber}";

        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);

        double margin = 40;
        double y = 40;
        double width = page.Width.Point - (margin * 2);

        // Fonts
        var fontTitle = new XFont("Arial", 14, XFontStyleEx.Bold);
        var fontHeader = new XFont("Arial", 11, XFontStyleEx.Bold);
        var fontBold = new XFont("Arial", 10, XFontStyleEx.Bold);
        var fontRegular = new XFont("Arial", 10, XFontStyleEx.Regular);
        var fontSmall = new XFont("Arial", 9, XFontStyleEx.Regular);

        // Helper to draw text
        void DrawText(string text, XFont font, double xOffset = 0, XBrush? brush = null)
        {
            brush ??= XBrushes.Black;
            gfx.DrawString(text, font, brush, margin + xOffset, y);
            y += font.Size + 4;
        }

        void DrawCenterText(string text, XFont font, XBrush? brush = null)
        {
            brush ??= XBrushes.Black;
            var size = gfx.MeasureString(text, font);
            gfx.DrawString(text, font, brush, margin + (width - size.Width) / 2, y);
            y += font.Size + 4;
        }

        void DrawDivider()
        {
            y += 4;
            gfx.DrawLine(XPens.Gray, margin, y, page.Width.Point - margin, y);
            y += 8;
        }

        // Header
        DrawCenterText("GOVERNMENT OF INDIA", fontBold);
        DrawCenterText("DISTRICT COURT OF KOCHI", fontTitle);
        DrawCenterText("KERALA, INDIA", fontSmall);
        y += 10;

        DrawDivider();
        DrawCenterText("COURT ORDER", fontTitle);
        y += 10;

        // Order & Date Info
        var dateStr = DateTime.UtcNow.ToString("dd MMMM yyyy");
        var orderNo = caseNumber.Replace("CCMS-", "CO-");

        gfx.DrawString($"Order No        : {orderNo}", fontBold, XBrushes.Black, margin, y);
        gfx.DrawString($"Order Date      : {dateStr}", fontBold, XBrushes.Black, margin + 300, y);
        y += 14;

        gfx.DrawString($"CCMS Case No    : {caseNumber}", fontBold, XBrushes.Black, margin, y);
        y += 10;

        DrawDivider();

        // 1. COURT DETAILS
        DrawText("1. COURT DETAILS", fontHeader);
        y += 4;
        DrawText("Court Name       : District Court of Kochi", fontRegular, 10);
        DrawText("Presiding Officer: Hon. Judge Rajesh Kumar", fontRegular, 10);
        y += 6;

        DrawDivider();

        // 2. COMPLAINANT DETAILS
        DrawText("2. COMPLAINANT DETAILS", fontHeader);
        y += 4;
        DrawText($"Name             : {dto.ComplainantName}", fontRegular, 10);
        DrawText("Address          : 45, MG Road, Kochi, Kerala", fontRegular, 10);
        DrawText("Contact Number   : +91-98XXXXXXXX", fontRegular, 10);
        y += 6;

        DrawDivider();

        // 3. DEFENDANT DETAILS
        DrawText("3. DEFENDANT DETAILS", fontHeader);
        y += 4;
        DrawText($"Name             : {dto.DefendantName}", fontRegular, 10);

        string aadhaarVal = "XXXX XXXX XXXX";
        string panVal = "XXXXXXXXXX";
        if (dto.DefendantId.Length == 12)
        {
            aadhaarVal = dto.DefendantId;
        }
        else if (dto.DefendantId.Length == 10)
        {
            panVal = dto.DefendantId;
        }
        DrawText($"Aadhaar Number   : {aadhaarVal}", fontRegular, 10);
        DrawText($"PAN Number       : {panVal}", fontRegular, 10);
        DrawText($"Bank Name        : {dto.DefendantBankName}", fontRegular, 10);
        DrawText($"Account Number   : {dto.DefendantAccountNumber}", fontRegular, 10);
        y += 6;

        DrawDivider();

        // 4. SUBJECT OF ORDER
        DrawText("4. SUBJECT OF ORDER", fontHeader);
        y += 4;
        
        string orderType = dto.OrderType.Equals("BalanceEnquiry", StringComparison.OrdinalIgnoreCase) ? "BALANCE ENQUIRY" : "FREEZE ACCOUNT";
        DrawText($"Order Type       : {orderType}", fontBold, 10);

        if (orderType == "FREEZE ACCOUNT")
        {
            var amount = dto.FreezeAmount ?? 0;
            DrawText($"Freeze Amount    : INR {amount:N2}", fontBold, 10);
            y += 4;

            // Description block
            string desc1 = "This court has examined the matter presented by the complainant and hereby issues the";
            string desc2 = $"following order. The concerned bank is directed to freeze an amount of INR {amount:N2}";
            string desc3 = "from the above-mentioned account until further instructions are issued by the court.";

            DrawText(desc1, fontRegular, 10);
            DrawText(desc2, fontRegular, 10);
            DrawText(desc3, fontRegular, 10);
        }
        else
        {
            y += 4;
            string desc1 = "The concerned bank is instructed to verify the account details mentioned in this order";
            string desc2 = "and provide the latest available account balance through the Court Case Management System.";
            string desc3 = "No account freeze is required under this order.";

            DrawText(desc1, fontRegular, 10);
            DrawText(desc2, fontRegular, 10);
            DrawText(desc3, fontRegular, 10);
        }
        y += 6;

        DrawDivider();

        // 5. INSTRUCTIONS TO THE BANK
        DrawText("5. INSTRUCTIONS TO THE BANK", fontHeader);
        y += 4;
        DrawText("- Verify the customer account details against bank records.", fontRegular, 10);
        if (orderType == "FREEZE ACCOUNT")
        {
            DrawText("- Apply the freeze amount as instructed by the court.", fontRegular, 10);
        }
        else
        {
            DrawText("- Retrieve the current account balance as requested.", fontRegular, 10);
        }
        DrawText("- Record the action in the Court Case Management System.", fontRegular, 10);
        DrawText("- Submit the official response through the CCMS portal.", fontRegular, 10);
        y += 6;

        DrawDivider();

        // 6. LEGAL DECLARATION
        DrawText("6. LEGAL DECLARATION", fontHeader);
        y += 4;
        DrawText("This order is issued under the authority vested in the District Court and shall be treated", fontRegular, 10);
        DrawText("as a legally binding instruction. Failure to comply may result in legal proceedings.", fontRegular, 10);
        y += 10;

        DrawDivider();

        // Sign-off section
        gfx.DrawString("Issued By:", fontRegular, XBrushes.Black, margin, y);
        gfx.DrawString("Signature: [Signed]", fontRegular, XBrushes.Black, margin, y + 20);
        gfx.DrawString("Judge Name: Hon. Rajesh Kumar", fontBold, XBrushes.Black, margin, y + 40);
        gfx.DrawString("Court Seal: [Official Court Seal]", fontRegular, XBrushes.Black, margin, y + 60);

        gfx.DrawString("Date of Issue:", fontRegular, XBrushes.Black, margin + 300, y);
        gfx.DrawString(dateStr, fontBold, XBrushes.Black, margin + 300, y + 20);
        y += 85;

        DrawDivider();
        DrawCenterText("END OF DOCUMENT", fontSmall, XBrushes.DarkGray);

        using var ms = new MemoryStream();
        document.Save(ms);
        return ms.ToArray();
    }

    public byte[] GenerateSimulatedPdf(string fileName)
    {
        // Initialize FailsafeFontResolver for Linux environments
        GlobalFontSettings.FontResolver ??= new FailsafeFontResolver();

        var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        var font = new XFont("Arial", 16, XFontStyleEx.Bold);
        gfx.DrawString($"Simulated Document: {fileName}", font, XBrushes.Black, new XRect(0, 0, page.Width, page.Height), XStringFormats.Center);
        
        using var ms = new MemoryStream();
        document.Save(ms);
        return ms.ToArray();
    }
}
