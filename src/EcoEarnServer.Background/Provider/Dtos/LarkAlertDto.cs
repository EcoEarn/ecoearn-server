using System;

namespace EcoEarnServer.Background.Provider.Dtos;

public class LarkAlertDto
{
    public LarkAlertMsgType MsgType { get; set; }
    public string Content { get; set; }

    public string GetMsgTypeStr()
    {
        switch (MsgType)
        {
            case LarkAlertMsgType.Text:
                return "text";
            case LarkAlertMsgType.Post:
                return "post";
            case LarkAlertMsgType.ShareChat:
                return "share_chat";
            case LarkAlertMsgType.Image:
                return "Image";
            case LarkAlertMsgType.Interactive:
                return "interactive";
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

public enum LarkAlertMsgType
{
    Text,
    Post,
    ShareChat,
    Image,
    Interactive
}

public class LarkAlertResDto
{
    public long Code { get; set; }
    public string Msg { get; set; }
}