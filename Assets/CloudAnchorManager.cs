using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Google ARCore Extensions
using Google.XR.ARCoreExtensions;
using System;
using TMPro;

public class CloudAnchorManager : MonoBehaviour
{
    // 상태 변수
    public enum Mode { READY, HOST, HOST_PENDING, RESOLVE, RESOLVE_PENDING };

    // 버튼
    public Button hostButton;       // 클라우드 앵커 등록
    public Button resolveButton;    // 클라우드 앵커 조회
    public Button resetButton;      // 리셋

    // 메시지 출력 텍스트
    public TMP_Text messageText;

    // 상태변수
    public Mode mode = Mode.READY;
    // AnchorManager    // 로컬 앵커를 생성하기 위한 클래스
    public ARAnchorManager anchorManager;
    // ArRaycastManager
    public ARRaycastManager raycastManager;

    // 증강시킬 객체 프리팹
    public GameObject anchorPrefab;
    // 저장 객체 변수 (삭제하기 위한 용도)
    private GameObject anchorGameObject;

    // 로컬앵커 저장 변수
    private ARAnchor localAnchor;
    // 클라우드 앵커 변수
    private ARCloudAnchor cloudAnchor;


    // Raycast Hit
    private List<ARRaycastHit> hits = new List<ARRaycastHit>();

    // 클라우드 앵커 ID 저장하기 위한 키 값 (PlayerPregs.SetString("키", 값));
    private const string cloudAnchorKey = "CLOUD_ANCHOR_ID";
    // 클라우드 앵커 ID
    private string strCloudAnchorId;


    void Start()
    {
        // 버튼 이벤트 연결
        hostButton.onClick.AddListener(() => OnHostClick());
        resolveButton.onClick.AddListener(() => OnResolveClick());
        resetButton.onClick.AddListener(() => OnResetClick());

        strCloudAnchorId = PlayerPrefs.GetString(cloudAnchorKey, "");
    }

    void Update()
    {
        if (mode == Mode.HOST)
        {
            Hosting();
            HostProcessing();
        }
        if (mode == Mode.HOST_PENDING)
        {
            HostPending();
        }
        if (mode == Mode.RESOLVE)
        {
            Resolving();
        }
        if (mode == Mode.RESOLVE_PENDING)
        {
            ResolvePending();
        }

    }

    void Hosting()
    {
        if (Input.touchCount < 1) return;

        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Began) return;


        // 로컬 앵커가 존재하는지 여부를 확인
        if (localAnchor == null)
        {
            // Raycast 발사
            if (raycastManager.Raycast(touch.position, hits, TrackableType.PlaneWithinPolygon))
            {
                // 로컬 앵커를 생성
                localAnchor = anchorManager.AddAnchor(hits[0].pose);
                // 로컬 앵커 위치에 사슴 증강시키고 변수에 저장
                anchorGameObject = Instantiate(anchorPrefab, localAnchor.transform);
            }
        }
    }

    // 클라우드 앵커 등록
    void HostProcessing()
    {
        if (localAnchor == null) return;

        // 피쳐포인트의 갯수 및 퀄리티 측정
        FeatureMapQuality quality = anchorManager.EstimateFeatureMapQualityForHosting(GetCameraPose());

        string mappingText = string.Format("맵핑 품질 = {0}", quality);

        // 맵핑 퀄리티가 1 이상일 때 호스팅 요청
        if (quality == FeatureMapQuality.Sufficient || quality == FeatureMapQuality.Good)
        {
            // 1일짜리 앵커포인트
            cloudAnchor = anchorManager.HostCloudAnchor(localAnchor, 1);

            if (cloudAnchor == null)
            {
                mappingText = "클라우드 앵커 생성 실패";
            }
            else
            {
                // 여기서 클라우드 앵커 ID 찍어도 안나옴
                // 서버에서 작업하는 시간이 있기 때문에
                mappingText = "클라우드 앵커 생성 시작";
                mode = Mode.HOST_PENDING;
            }
        }

        messageText.text = mappingText;

    }

    void HostPending()
    {
        string mappingText = "";
        if (cloudAnchor.cloudAnchorState == CloudAnchorState.Success)
        {
            mappingText = $"클라우드 앵커 생성 성공, CloudAnchor ID = {cloudAnchor.cloudAnchorId}";
            // 앵커ID 저장
            strCloudAnchorId = cloudAnchor.cloudAnchorId;

            // 클라우드 앵커 ID를 공유하는 로직
            // 포톤 네트워크 : RPC 호출
            // 파이어베이스 : 데이터 저장
            PlayerPrefs.SetString(cloudAnchorKey, strCloudAnchorId);

            // 초기화
            cloudAnchor = null;
            // 기존에 증강된 객체를 삭제
            Destroy(anchorGameObject);

            mode = Mode.READY;
        }
        else
        {
            mappingText = $"클라우드 앵커 생성 진행중...{cloudAnchor.cloudAnchorState}";
        }

        messageText.text = mappingText;
    }

    void Resolving()
    {
        if (PlayerPrefs.HasKey(cloudAnchorKey) == false) return;
        messageText.text = "";
        // 클라우드 앵커 ID 받아옴. (포톤, 파이어베이스)
        strCloudAnchorId = PlayerPrefs.GetString(cloudAnchorKey);

        // 클라우드 앵커 ID로 CloudAnchor 로드
        cloudAnchor = anchorManager.ResolveCloudAnchorId(strCloudAnchorId);

        if (cloudAnchor == null)
        {
            messageText.text = "클라우드 앵커 리졸브 실패";
        }
        else
        {
            messageText.text = $"클라우드 앵커 리졸브 성공 : {cloudAnchor.cloudAnchorId}";
            // id만 가져온 것
            // 피쳐포인트를 후속으로 가져옴
            mode = Mode.RESOLVE_PENDING;
        }
    }

    void ResolvePending()
    {
        if (cloudAnchor.cloudAnchorState == CloudAnchorState.Success)
        {
            messageText.text = "리졸브 성공";

            // 객체 증강
            anchorGameObject = Instantiate(anchorPrefab, cloudAnchor.transform);
            mode = Mode.READY;
        }
        else
        {
            messageText.text = $"리졸빙 진행 중...{cloudAnchor.cloudAnchorState}";
        }
    }


    // MainCamera 태그로 지정된 카메라의 위치와 각도를 Pose 데이터 타입으로 반환
    public Pose GetCameraPose()
    {
        return new Pose(Camera.main.transform.position, Camera.main.transform.rotation);
    }


    private void OnHostClick()
    {
        mode = Mode.HOST;
    }

    private void OnResolveClick()
    {
        mode = Mode.RESOLVE;
    }

    private void OnResetClick()
    {
        if (anchorGameObject != null)
        {
            Destroy(anchorGameObject);
        }
        cloudAnchor = null;
        localAnchor = null;
        messageText.text = "준비완료";
        mode = Mode.READY;
    }
}