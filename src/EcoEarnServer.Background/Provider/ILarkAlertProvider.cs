using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using EcoEarnServer.Background.Options;
using EcoEarnServer.Background.Provider.Dtos;
using EcoEarnServer.Common.HttpClient;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;

namespace EcoEarnServer.Background.Provider;

public interface ILarkAlertProvider
{
    Task SendLarkAlertAsync(LarkAlertDto dto);
    Task SendLarkFailAlertAsync(string failMessage);
}

public class LarkAlertProvider : ILarkAlertProvider, ISingletonDependency
{
    private readonly IHttpProvider _httpProvider;
    private readonly LarkAlertOptions _larkAlertOptions;

    public LarkAlertProvider(IHttpProvider httpProvider, IOptionsSnapshot<LarkAlertOptions> larkAlertOptions)
    {
        _httpProvider = httpProvider;
        _larkAlertOptions = larkAlertOptions.Value;
    }

    public async Task SendLarkAlertAsync(LarkAlertDto dto)
    {
        await _httpProvider.InvokeAsync<LarkAlertResDto>(HttpMethod.Post, _larkAlertOptions.BaseUrl,
            param: new Dictionary<string, string>
            {
                ["msg_type"] = dto.GetMsgTypeStr(),
                ["content"] = dto.Content
            }, header: null);
    }

    public async Task SendLarkFailAlertAsync(string failMessage)
    {
        var content = new Dictionary<string, string>
        {
            ["text"] = failMessage,
        };
        var larkAlertDto = new LarkAlertDto
        {
            MsgType = LarkAlertMsgType.Text,
            Content = JsonConvert.SerializeObject(content)
        };
        await SendLarkAlertAsync(larkAlertDto);
    }
}