using UnityEngine;

namespace ComponentControl
{
    // 자식 오브젝트 중 원하는 대상을 특정하기 위한 마커용 컴포넌트
    // 이 스크립트를 찾고 싶은 자식 오브젝트에 붙여두면
    // GetComponentInChildren<Parent>() 로 그 오브젝트를 찾아낼 수 있음
    public class Parent : MonoBehaviour
    {
    }
}
