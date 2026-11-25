// 소리. 웹 game.js의 Snd/sfx/loopEl/syncAudio를 옮겼다.
// 배경음·환경음은 소리마다 하나씩 두고 멈췄다가 이어서 튼다(웹 audio 요소의 pause/play와 같다).
// 효과음은 소리마다 3개를 돌려 쓰며 처음부터 다시 튼다.
using System.Collections.Generic;
using UnityEngine;

namespace Stairs.Game
{
    public class AudioDirector : MonoBehaviour
    {
        public SpriteCatalog catalog;
        public bool muted;
        [Range(0, 1)] public float vol = 1f;   // 웹 Snd.vol (설정 화면의 볼륨, 0.1 단위)

        readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();
        readonly HashSet<string> paused = new HashSet<string>();
        readonly Dictionary<string, AudioSource[]> pools = new Dictionary<string, AudioSource[]>();
        readonly Dictionary<string, int> rr = new Dictionary<string, int>();

        AudioClip Clip(string id) { var s = catalog ? catalog.GetSound(id) : null; return s != null ? s.clip : null; }
        public bool Has(string id) => catalog && catalog.GetSound(id) != null;

        AudioSource NewSource(AudioClip c, bool loop)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.clip = c; a.loop = loop; a.playOnAwake = false;
            return a;
        }

        /// <summary>웹 sfx(id): 음소거가 아니면 그 소리의 3개 중 다음 것을 처음부터 튼다.</summary>
        /// <summary>플레이테스트 기록용: 효과음별로 실제로 튼 횟수.</summary>
        public readonly SortedDictionary<string, int> played = new SortedDictionary<string, int>();

        public void Sfx(string id, float sfxVolume)
        {
            if (muted) return;
            if (!pools.TryGetValue(id, out var pool))
            {
                var c = Clip(id);
                pool = c ? new[] { NewSource(c, false), NewSource(c, false), NewSource(c, false) } : new AudioSource[0];
                pools[id] = pool; rr[id] = 0;
            }
            if (pool.Length == 0) return;
            rr[id] = (rr[id] + 1) % pool.Length;
            var a = pool[rr[id]];
            a.volume = Mathf.Clamp01(sfxVolume * vol);
            a.Stop(); a.time = 0; a.Play();
            played[id] = played.TryGetValue(id, out var n) ? n + 1 : 1;
        }

        AudioSource Loop(string id)
        {
            if (!loops.TryGetValue(id, out var a))
            {
                var c = Clip(id);
                a = c ? NewSource(c, true) : null;
                loops[id] = a;
            }
            return a;
        }

        /// <summary>플레이테스트 기록용: 지금 울리는 반복 소리들.</summary>
        public string Describe()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in loops) if (kv.Value && kv.Value.isPlaying) sb.Append(kv.Key).Append('(').Append(kv.Value.volume.ToString("0.00")).Append(" t=").Append(kv.Value.time.ToString("0.00")).Append(' ').Append(kv.Value.clip.loadState).Append(") ");
            return sb.Length > 0 ? sb.ToString().TrimEnd() : "없음";
        }

        /// <summary>웹 syncAudio의 반복문: want에 있는 소리는 그 볼륨으로 (멈춰 있었으면 이어서) 틀고, 나머지는 멈춘다.
        /// 구간 음악(bgm_stage*)은 한 번도 안 쓴 것이면 만들지 않는다.</summary>
        public void SyncLoops(IEnumerable<string> ids, Dictionary<string, float> want)
        {
            foreach (var id in ids)
            {
                if (id.StartsWith("bgm_stage") && !want.ContainsKey(id) && !loops.ContainsKey(id)) continue;
                var a = Loop(id);
                if (!a) continue;
                if (want.TryGetValue(id, out var v))
                {
                    a.volume = Mathf.Clamp01(v * vol);
                    if (!a.isPlaying)
                    {
                        if (paused.Remove(id)) a.UnPause(); else a.Play();
                    }
                }
                else if (a.isPlaying) { a.Pause(); paused.Add(id); }
            }
        }
    }
}
