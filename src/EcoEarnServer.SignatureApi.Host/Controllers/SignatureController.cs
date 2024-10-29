using System;
using System.Threading.Tasks;
using AElf;
using EcoEarnServer.SignatureServer.Dtos;
using EcoEarnServer.SignatureServer.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace EcoEarnServer.SignatureServer.Controllers;

[RemoteService]
[ApiController]
[Route("api/app/signature")]
public class SignatureController : AbpControllerBase
{
    private readonly ILogger<SignatureController> _logger;
    private readonly AccountProvider _accountProvider;
    private readonly ISignatureProvider _signatureProvider;

    public SignatureController(ILogger<SignatureController> logger,
        AccountProvider accountProvider, 
        ISignatureProvider signatureProvider)
    {
        _logger = logger;
        _accountProvider = accountProvider;
        _signatureProvider = signatureProvider;
        LocalizationResource = typeof(EcoEarnServerSignatureResource);
    }

    [HttpPost]
    public async Task<SignResponseDto> SendSignAsync(
        SendSignatureDto input)
    {
        try
        {
            _logger.LogDebug("input Account: {Account}, HexMsg: {HexMsg}", input.Account, input.HexMsg);
            var signatureResult = _accountProvider.GetSignature(input.Account,
                ByteArrayHelper.HexStringToByteArray(input.HexMsg));
            _logger.LogDebug("Signature result :{SignatureResult}", signatureResult);

            return new SignResponseDto
            {
                Signature = signatureResult,
            };
        }
        catch (Exception e)
        {
            _logger.LogError("Signature failed, error msg is {ErrorMsg}", e);
            throw new UserFriendlyException(e.Message);
        }
    }

    [HttpPost("thirdPart")]
    public async Task<SignResponseDto> SendThirdPartSignAsync(SignDto input)
    {
        return _signatureProvider.SignThirdPart(input);
    }
}