using UnityEngine;

public class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
{
    // Scene 전환 시 삭제 여부
    protected bool _IsDestroyOnLoad = false;

    // 이 클래스의 스태틱 인스턴스 변수
    protected static T _Instance;
    public static T Instance => _Instance;

    private void Awake()
    {
        Init();
    }

    // 상속받아 추가 기능 구현 가능하도록 구현
    protected virtual void Init()
    {
        if (_Instance == null)
        {
            _Instance = (T)this;

            if (!_IsDestroyOnLoad)
            {
                DontDestroyOnLoad(this);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 삭제 시 실행되는 함수
    protected virtual void OnDestroy()
    {
        Dispose();
    }

    // 삭제 시 추가 작업 기능은 Dispose 내 구현
    protected virtual void Dispose()
    {
        _Instance = null;
    }
}