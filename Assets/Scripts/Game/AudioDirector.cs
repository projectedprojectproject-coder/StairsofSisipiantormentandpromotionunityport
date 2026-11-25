// 배경음악(20층마다 bgm_stage1~5)과 효과음. 웹의 setBgm/stageBgm과 sfx에 해당한다.
using UnityEngine;

namespace Stairs.Game
{
    public class AudioDirector : MonoBehaviour
    {
        public SpriteCatalog catalog;
        [Range(0, 1)] public float bgmVolume = 0.45f, ambVolume = 0.15f, sfxVolume = 0.8f;
        public bool muted;

        AudioSource bgm, amb, sfx;
        string bgmId;

        void Awake()
        {
            bgm = gameObject.AddComponent<AudioSource>(); bgm.loop = true; bgm.playOnAwake = false;
            amb = gameObject.AddComponent<AudioSource>(); amb.loop = true; amb.playOnAwake = false;
            sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false;
        }

        /// <summary>배경음악을 바꾼다. 같은 곡이면 그대로 두고, 바뀌면 처음부터 튼다.</summary>
        public void SetBgm(string id)
        {
            if (id == bgmId) return;
            bgmId = id;
            var s = id != null && catalog ? catalog.GetSound(id) : null;
            bgm.Stop();
            if (s == null || s.clip == null) return;
            bgm.clip = s.clip; bgm.volume = muted ? 0 : bgmVolume; bgm.Play();
        }

        public void SetAmbience(string id)
        {
            var s = id != null && catalog ? catalog.GetSound(id) : null;
            if (s == null || s.clip == null) { amb.Stop(); return; }
            if (amb.clip == s.clip && amb.isPlaying) return;
            amb.clip = s.clip; amb.volume = muted ? 0 : ambVolume; amb.Play();
        }

        /// <summary>소리 켜기·끄기 (일시정지 메뉴, SaveData에 남는다).</summary>
        public void SetMuted(bool m)
        {
            muted = m;
            if (bgm) bgm.volume = m ? 0 : bgmVolume;
            if (amb) amb.volume = m ? 0 : ambVolume;
        }

        public void Play(string id, float vol = 1f)
        {
            if (muted || !catalog) return;
            var s = catalog.GetSound(id);
            if (s != null && s.clip) sfx.PlayOneShot(s.clip, sfxVolume * vol);
        }

        /// <summary>층에 맞는 단계 곡 id (없으면 bgm_loop).</summary>
        public string StageBgm(int floor)
        {
            string id = "bgm_stage" + Stairs.Core.StairLayout.StageOf(floor);
            return catalog && catalog.GetSound(id) != null ? id : "bgm_loop";
        }
    }
}
