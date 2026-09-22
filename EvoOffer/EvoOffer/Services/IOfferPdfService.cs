using EvoOffer.Models;

namespace EvoOffer.Services;

public interface IOfferPdfService
{
    /// <summary>QuestPDF supports desktop/server runtimes, but not iOS, Android or Mac Catalyst.</summary>
    bool IsSupported { get; }

    byte[] Generate(OfferPdfData offer, OfferPdfOptions? options = null);

    /// <summary>Writes the PDF without closing the caller-owned stream.</summary>
    void Generate(OfferPdfData offer, Stream output, OfferPdfOptions? options = null);
}
