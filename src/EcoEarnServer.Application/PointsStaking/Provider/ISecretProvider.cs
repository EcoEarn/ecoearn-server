using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Types;
using EcoEarnServer.Common;
using EcoEarnServer.Common.Dtos;
using EcoEarnServer.Common.HttpClient;
using EcoEarnServer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using HttpMethod = System.Net.Http.HttpMethod;

namespace EcoEarnServer.PointsStaking.Provider;

public interface ISecretProvider
{
    Task<string> GetSignatureFromHashAsync(string publicKey, Hash hash);
}

public class SecretProvider : ISecretProvider, ISingletonDependency
{
    private const string GetSecurityUri = "/api/app/thirdPart/secret";
    private const string GetSignatureUri = "/api/app/signature";


    private readonly ILogger<SecretProvider> _logger;
    private readonly IOptionsMonitor<SecurityServerOptions> _securityOption;
    private readonly IHttpProvider _httpProvider;

    public SecretProvider(ILogger<SecretProvider> logger, IOptionsMonitor<SecurityServerOptions> securityOption,
        IHttpProvider httpProvider)
    {
        _logger = logger;
        _securityOption = securityOption;
        _httpProvider = httpProvider;
    }

    private string Uri(string path)
    {
        return _securityOption.CurrentValue.BaseUrl.TrimEnd('/') + path;
    }

    public async Task<string> GetSignatureFromHashAsync(string publicKey, Hash hash)
    {
        try
        {
            var signatureSend = new SendSignatureDto
            {
                PublicKey = publicKey,
                HexMsg = hash.ToHex(),
            };

            var url = Uri(GetSignatureUri);
            var resp = await _httpProvider.InvokeAsync<CommonResponseDto<SignResponseDto>>(HttpMethod.Post,
                url,
                body: JsonConvert.SerializeObject(signatureSend),
                header: SecurityServerHeader()
            );
            AssertHelper.IsTrue(resp?.Success ?? false, "Signature response failed");
            AssertHelper.NotEmpty(resp!.Data?.Signature, "Signature response empty");
            return resp.Data!.Signature;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "CallSignatureServiceFailed, err: {err}, hash: {body}", e.ToString(),
                JsonConvert.SerializeObject(hash.ToHex()));
            return null;
        }
    }

    private async Task<string> GetSecretAsync(string key)
    {
        var resp = await _httpProvider.InvokeAsync<CommonResponseDto<string>>(HttpMethod.Get,
            Uri(GetSecurityUri),
            param: new Dictionary<string, string>
            {
                ["key"] = key
            },
            header: SecurityServerHeader(key));
        AssertHelper.NotEmpty(resp?.Data, "Secret response data empty");
        AssertHelper.IsTrue(resp!.Success, "Secret response failed {}", resp.Message);
        return EncryptionHelper.DecryptFromHex(resp!.Data, _securityOption.CurrentValue.AppSecret);
    }

    public Dictionary<string, string> SecurityServerHeader(params string[] signValues)
    {
        var signString = string.Join(CommonConstant.EmptyString, signValues);
        return new Dictionary<string, string>
        {
            ["appid"] = _securityOption.CurrentValue.AppId,
            ["signature"] = EncryptionHelper.EncryptHex(signString, _securityOption.CurrentValue.AppSecret)
        };
    }


    public class SendSignatureDto
    {
        public string PublicKey { get; set; }
        public string HexMsg { get; set; }
    }

    public class SignResponseDto
    {
        public string Signature { get; set; }
    }
}