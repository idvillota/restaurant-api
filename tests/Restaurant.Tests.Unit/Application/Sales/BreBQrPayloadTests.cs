using Restaurant.Application.Features.Sales.Bills;
using Restaurant.Domain.Enums;

namespace Restaurant.Tests.Unit.Application.Sales;

public sealed class BreBQrPayloadTests
{
    private const string KnownQr =
        "00020101021226300014CO.COM.ACH.LLA0408@kamin0149250014CO.COM.ACH.RED0103ACH50290013CO.COM.ACH.CU0108@kamin01520400005303170540410005802CO5905Kamin60051100161051100162140704000008020080270016CO.COM.ACH.CANAL0103APP81250015CO.COM.ACH.CIVA01020282230014CO.COM.ACH.IVA0101083240015CO.COM.ACH.BASE0101084250015CO.COM.ACH.CINC01020285230014CO.COM.ACH.INC0101090300016CO.COM.ACH.TRXID010620004091460014CO.COM.ACH.SEC0124RKF/mxYcjwdvtISAkrIbpqap63042B49";

    [Fact]
    public void Crc16_matches_the_standard_vector() =>
        Assert.Equal("29B1", BreBQrPayload.Crc16Ccitt("123456789"));

    [Fact]
    public void Crc16_matches_a_published_breb_qr() =>
        Assert.Equal("2B49", BreBQrPayload.Crc16Ccitt(KnownQr[..^4]));

    [Fact]
    public void Build_locks_the_transfer_amount_and_key()
    {
        var payload = BreBQrPayload.Build(new BreBQrInput(
            "@localkey",
            BreBKeyType.Alphanumeric,
            15000m,
            "José Café",
            "Bogotá",
            "11001",
            "ABC123"));

        Assert.Contains("010211", payload);
        Assert.Contains("0409@localkey", payload);
        Assert.Contains("540815000.00", payload);
        Assert.Contains("5909Jose Cafe", payload);
        Assert.Contains("6006Bogota", payload);
        Assert.Contains("610511001", payload);
        Assert.EndsWith(BreBQrPayload.Crc16Ccitt(payload[..^4]), payload);
        Assert.DoesNotContain("6304", payload[..^8]);
    }

    [Fact]
    public void Build_rejects_a_blank_key()
    {
        var error = Assert.Throws<InvalidOperationException>(() => BreBQrPayload.Build(new BreBQrInput(
            "  ",
            BreBKeyType.Phone,
            10m,
            "Local",
            "Cali",
            null,
            "REF1")));

        Assert.Contains("llave", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
