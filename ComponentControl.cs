using Unity.VisualScripting;
using UnityEngine;
namespace ComponentControl
{
    public class ComponentControl : MonoBehaviour
    {
        // 컴포넌트 유니티는 컴포넌트 단위로 이루어진 게임 엔진임
        // 즉 컴포넌트를 가져와서 값을 수정해야 함

        // Transform 컴포넌트를 받을 수 있는 상자(변수) 준비

        // 일단, 변수는 private으로 막아두는 게 좋음
        // 근데 유니티의 Inspector 에서 값을 넣으려면
        // private 변수는 값을 넣을 수 없음
        // 그때 유니티 엔진에서만 해당 private 변수에 접근
        // 할 수 있게 해주는 게 [SerializeField]
        // 라는 어트리뷰트(Attribute)임
        [SerializeField] private Transform myTransform = null;
        // [SerializeField] 와 private을 조합하여 유니티의
        // 인스펙터에서 Component의 참조를 넣으면
        // 아주 편하고 잘 된 것처럼 보이지만
        // 변수의 이름을 바꿀거나 했을 때 넣어뒀던 Component가 없어져버림
        // 그래서 코드로 찾아오는 것이 좋음
        // (근데 기능 구현이 급하거나 했을 때는
        // 일단 public이든 뭐든 써서 구현하는 게 제일 좋음.)


        [SerializeField] private Transform parentTransform = null;

        [SerializeField]
        private Transform[] TransformArray = null;
        private void Awake()
        {
            // 컴포넌트를 가져오는 기능
            // GetComponent
            // GetComponent<가져오고 싶은 컴포넌트의 타입>
            // GetComponent는 본인의 오브젝트의 컴포넌트를 가져올 수 있는 기능임
            // 반환값으로는 <> 안에 넣어둔 클래스의 인스턴스가 반환됨.
            // (컴포넌트를 가져온다고 생각.)
            myTransform = GetComponent<Transform>();

            // GetComponentInChildren<가져오고 싶은 타입>
            // 본인도 자식으로 포함시킨다는 거임
            // 그래서 자식 오브젝트의 컴포넌트를 가져오려면
            // GetComponentInChildren<자식의 컴포넌트 타입>
            // 을 먼저 가져와서 자식 오브젝트를 특정한 다음
            // 그 특정된 자식 오브젝트한테 GetComponent를 하라고 하면
            // 원하는 자식 오브젝트의 Transform을 가져올 수 있음
            parentTransform
                = GetComponentInChildren<Parent>()
                 .GetComponentInChildren<Transform>();

            // 오브젝트의 리음을 가져오는 방법
            // GetComponentInChildren<>
            // 자식의 컴포넌트를 가져오는 방법
            // 근데 특징이 자기 자신도 자식에 포함이 됨
            // 그래서 parentTransfrom.neme을 출력하니까
            // 본인 이름이 뜸
            Debug.Log(parentTransform.name);

            // parent, Child가 있는데
            // 그녀석들의 Transform을 다 가져오고 싶음
            // Transform ParentTransform
            // Transform  ChildTransform

            // int a = 1;
            // int b = 2;
            // int [] somearray = {1,2}

            // GetComponentsInChildren 도 마찬가지로
            // 본인 오브젝트가 자식에 포함이 됨
            // 그래서 자식 오브젝트들만 가져오고 싶다면
            // 추상화된 클래스 타입으로 가져오는 방식 또는
            // 0번째 오브젝트(본인 오브젝트)를 제외하고 가져오는
            // 방식을 사용해야 함 (추상화된 컴포넌트 형식으로 가져오는 방식을 권장.)

            TransformArray = GetComponentsInChildren<Transform>();

            for (int i = 0; i < TransformArray.Length; i++)
            {
                Debug.Log(TransformArray[i].name);
            }

            // FindObject 시리즈
            // 씬 전체에서 1개만 찾을 때
            // FindFirstObjectByType<찾고 싶은 컴포넌트의 타입>
            // Maincamera를 찾아서 Maincamera의 이름을 출력하고 싶음
            // 근데 Transform 기준으로 찾으면 모든 오브젝트가
            // Transform을 갖고 있기 때문에 딱 한개만 찾을 수가 없음
            // 그래서 카메라만 가지고 있는 카메라 컴포넌트를 찾아서
            // 그 안에 있는 트렌스 폼을 가져오는 방식

            // 씬에 있는 모든 오브젝트를 검사하는 함수이기 때문에 사용을 권장하지 않음
            // Awake나 start에서 한번정도 찾는건 괜찮음
            // (게임 로딩 중일때.)
            Camera camera = FindFirstObjectByType<Camera>();
            Transform cameraTransform
                = camera.GetComponent<Transform>();
            Debug.Log(cameraTransform.name);

            // FindAnyObjectByType()
            // 월드상에 특정 컴포넌트를 집히는 대로 하나만 가져옴
            // 매번 결과가 다를 수도 있음

            Transform anytransform
                = FindAnyObjectByType<Transform>();
            Debug.Log(anytransform.name);

            // Find 시리즈
            // 오브젝트의 이름을 기준으로 찾을거임
            GameObject mainCameraObject
                = GameObject.Find("Main Camera");
            // 1. 모든 오브젝트를 보는 게 문제임
            // 2. string 타입은 컴파일 타임에 오타를 안 잡아줌
            // 3. string 타입을 비교하여 같은 이름인지를 비교하기
            // 때문에 거의 쓰면 안됨.
            Debug.Log(mainCameraObject
                .GetComponent<Transform>()
                .name);

            GameObject childObject
                = GameObject.FindGameObjectWithTag("child");
            Debug.Log(childObject.name);



        }

        private void Start()
        {

            Debug.Log(myTransform.position);
            // Transform 컴포넌트의 포지션을 20,0,30으로 바꿈
            myTransform.position = new Vector3(20f, 0, 30f);


        }



    }

}
