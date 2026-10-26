namespace UnityCombatLab.Domain.Run
{
    /// <summary>
    /// 再現比較の成否・固定ハッシュ・最初の差の説明。
    /// [KO] 재현 비교 성공 여부·고정 해시·최초 차이 설명입니다.
    /// [EN] Replay comparison outcome, stable hashes and first-difference description.
    /// </summary>
    public sealed class ReplayVerificationResult
    {
        /// <summary>
        /// 全イベントと結果が一致したか。
        /// [KO] 모든 이벤트와 결과가 일치했는지 나타냅니다.
        /// [EN] Whether every event and the result matched.
        /// </summary>
        public bool IsMatch { get; }

        /// <summary>
        /// 保存記録の FNV-1a 64-bit ハッシュ。
        /// [KO] 저장 기록의 FNV-1a 64-bit 해시입니다.
        /// [EN] FNV-1a 64-bit hash of the recorded events.
        /// </summary>
        public ulong ExpectedHash { get; }

        /// <summary>
        /// 再計算イベントの同じ形式のハッシュ。
        /// [KO] 재계산 이벤트를 같은 형식으로 계산한 해시입니다.
        /// [EN] Hash of reproduced events in the same format.
        /// </summary>
        public ulong ActualHash { get; }

        /// <summary>
        /// 一致または最初の不一致の位置と内容。
        /// [KO] 일치 또는 최초 불일치 위치·내용입니다.
        /// [EN] Match report or the position and content of the first difference.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 比較結果の固定値を受け取ります。
        /// [KO] 비교 결과의 고정값을 받습니다.
        /// [EN] Receives immutable comparison results.
        /// </summary>
        internal ReplayVerificationResult(bool isMatch, ulong expectedHash, ulong actualHash, string message)
        {
            IsMatch = isMatch;
            ExpectedHash = expectedHash;
            ActualHash = actualHash;
            Message = message;
        }
    }
}
