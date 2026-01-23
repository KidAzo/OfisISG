using UnityEngine;

namespace WoiUtils.AudioSystem
{
    public class AudioPoolAdapter : MonoBehaviour
	{
		[SerializeField] AudioVoice voicePrefab;
		Pooling.IObjectPool<AudioVoice> pool;
        [SerializeField] AudioSystemConfig config;

        void Awake()
        {
            InitializePool();
        }

		void InitializePool()
		{
			pool = new Pooling.ObjectPool<AudioVoice>(
				CreateSoundEmitter,
				OnTakeFromPool,
				OnReturnedToPool,
				OnDestroyPoolObject,
				config.defaultCapacity,
				config.maxPoolSize);
		}

        AudioVoice CreateSoundEmitter()
        {
            var v = Instantiate(voicePrefab, transform);
            v.gameObject.SetActive(false);
            return v;
        }

        void OnTakeFromPool(AudioVoice v) => v.Get();
        void OnReturnedToPool(AudioVoice v) => v.Release();
        void OnDestroyPoolObject(AudioVoice v) => Destroy(v.gameObject);

        public AudioVoice Get() => pool.Get();

        public void Return(AudioVoice voice)
        {
            if (voice == null) return;
            
            if (AudioSystem.IsShuttingDown) return;
            
            if (!voice || voice.gameObject == null) return;
            
            pool.ReturnToPool(voice);
        }
	}
}