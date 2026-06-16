using System;
using AutoMapper;
using ccms_backend.dtos;
using ccms_backend.models;

namespace ccms_backend.services;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<CaseDocument, CaseDocumentDto>()
            .ForMember(dest => dest.DocumentType, opt => opt.MapFrom(src => src.DocumentType.ToString()))
            .ForMember(dest => dest.DownloadUrl, opt => opt.MapFrom(src => $"/api/attachments/{src.Id}/download"));

        CreateMap<CaseResponse, CaseResponseDto>()
            .ForMember(dest => dest.ResponseType, opt => opt.MapFrom(src => src.ResponseType.ToString()))
            .ForMember(dest => dest.ProcessedBy, opt => opt.MapFrom(src => src.RespondedByUser != null ? src.RespondedByUser.Username : null));

        CreateMap<Case, CaseSummaryDto>()
            .ForMember(dest => dest.OrderType, opt => opt.MapFrom(src => src.OrderType.ToString()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.DefendantName, opt => opt.MapFrom(src => src.Defendant != null ? src.Defendant.FullName : string.Empty))
            .ForMember(dest => dest.ValidationDate, opt => opt.MapFrom(src => src.ValidationResult != null ? (DateTime?)src.ValidationResult.ValidatedAt : null));

        CreateMap<Case, CaseDetailDto>()
            .ForMember(dest => dest.OrderType, opt => opt.MapFrom(src => src.OrderType.ToString()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.ComplainantName, opt => opt.MapFrom(src => src.Complainant != null ? src.Complainant.FullName : string.Empty))
            .ForMember(dest => dest.ComplainantIdentityNumber, opt => opt.MapFrom(src => src.Complainant != null ? src.Complainant.IdentityNumber : string.Empty))
            .ForMember(dest => dest.DefendantName, opt => opt.MapFrom(src => src.Defendant != null ? src.Defendant.FullName : string.Empty))
            .ForMember(dest => dest.DefendantAadhaar, opt => opt.MapFrom((src, dest) => src.Defendant != null ? DataMaskingHelper.MaskAadhaar(src.Defendant.IdentityNumber) : string.Empty))
            .ForMember(dest => dest.DefendantPan, opt => opt.MapFrom((src, dest) => src.Defendant != null ? DataMaskingHelper.MaskPan(src.Defendant.IdentityNumber) : string.Empty))
            .ForMember(dest => dest.DefendantAccountNumber, opt => opt.MapFrom((src, dest) => src.Defendant != null ? DataMaskingHelper.MaskAccount(src.Defendant.BankAccountNumber) : string.Empty))
            .ForMember(dest => dest.MatchedAccountNumber, opt => opt.MapFrom(src => src.ValidationResult != null ? src.ValidationResult.MatchedAccountNumber : null))
            .ForMember(dest => dest.AccountStatus, opt => opt.MapFrom(src => src.ValidationResult != null ? src.ValidationResult.AccountStatus : null))
            .ForMember(dest => dest.CurrentBalance, opt => opt.MapFrom(src => src.ValidationResult != null ? (decimal?)src.ValidationResult.CurrentBalance : null))
            .ForMember(dest => dest.ValidationTimestamp, opt => opt.MapFrom(src => src.ValidationResult != null ? (DateTime?)src.ValidationResult.ValidatedAt : null))
            .ForMember(dest => dest.Documents, opt => opt.MapFrom(src => src.Documents))
            .ForMember(dest => dest.Response, opt => opt.MapFrom(src => src.Response));
    }
}
