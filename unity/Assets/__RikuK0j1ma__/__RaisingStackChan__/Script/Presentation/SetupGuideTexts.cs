using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;

namespace RikuK0j1ma.RaisingStackChan.Presentation
{
    /// <summary>Japanese guide texts. Localisation is out of scope for now.</summary>
    public static class SetupGuideTexts
    {
        public static string For(SetupPhase phase, SetupFailure failure)
        {
            switch (phase)
            {
                case SetupPhase.PermissionExplanation:
                    return "StackChan の位置を知るために、次の許可が必要です。\n"
                         + "・カメラ: StackChan の QR コードを読み取る\n"
                         + "・空間データ: 部屋の中の位置を合わせる\n"
                         + "・アンカー: 位置を覚えて、次回から QR を省く\n"
                         + "このあと表示される確認で「許可」を選んでください。";
                case SetupPhase.LookAtFace:
                    return "StackChan の顔を見てください\n（画面に QR コードが出ています）";
                case SetupPhase.Detecting:
                    return "QR コードを検出しています…\nそのまま動かずに見続けてください";
                case SetupPhase.Detected:
                    return "見つかりました！";
                case SetupPhase.Connecting:
                    return "StackChan に接続しています…";
                case SetupPhase.Connected:
                    return "接続できました。球体を口に近づけてみましょう";
                case SetupPhase.Failed:
                    return ForFailure(failure);
                default:
                    return string.Empty;
            }
        }

        public static string ForFailure(SetupFailure failure)
        {
            switch (failure)
            {
                case SetupFailure.QrTooSmall:
                    return "QR コードが小さすぎます\nStackChan にもう少し近づいてください";
                case SetupFailure.QrTooDark:
                    return "QR コードが暗くて読めません\n部屋を明るくするか、画面の明るさを上げてください";
                case SetupFailure.QrGlare:
                    return "画面が反射して読めません\n少し角度を変えて見てください";
                case SetupFailure.WifiMismatch:
                    return "StackChan に届きません\nQuest と StackChan が同じ Wi-Fi か確認してください\n（ゲスト Wi-Fi は機器間通信を止めることがあります）";
                case SetupFailure.TokenRejected:
                    return "接続を拒否されました\nStackChan が再起動した可能性があります。QR をもう一度読んでください";
                default:
                    return string.Empty;
            }
        }

        public static string ForLink(StackChanConnectionState state)
        {
            switch (state)
            {
                case StackChanConnectionState.Connected: return "接続中";
                case StackChanConnectionState.Connecting: return "接続しています";
                case StackChanConnectionState.Reconnecting: return "再接続しています";
                default: return "未接続";
            }
        }
    }
}
