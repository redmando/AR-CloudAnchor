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
    // ���� ����
    public enum Mode { READY, HOST, HOST_PENDING, RESOLVE, RESOLVE_PENDING };

    // ��ư
    public Button hostButton;       // Ŭ���� ��Ŀ ���
    public Button resolveButton;    // Ŭ���� ��Ŀ ��ȸ
    public Button resetButton;      // ����

    // �޽��� ��� �ؽ�Ʈ
    public TMP_Text messageText;

    // ���º���
    public Mode mode = Mode.READY;
    // AnchorManager    // ���� ��Ŀ�� �����ϱ� ���� Ŭ����
    public ARAnchorManager anchorManager;
    // ArRaycastManager
    public ARRaycastManager raycastManager;

    // ������ų ��ü ������
    public GameObject anchorPrefab;
    // ���� ��ü ���� (�����ϱ� ���� �뵵)
    private GameObject anchorGameObject;

    // ���þ�Ŀ ���� ����
    private ARAnchor localAnchor;
    // Ŭ���� ��Ŀ ����
    private ARCloudAnchor cloudAnchor;


    // Raycast Hit
    private List<ARRaycastHit> hits = new List<ARRaycastHit>();

    // Ŭ���� ��Ŀ ID �����ϱ� ���� Ű �� (PlayerPregs.SetString("Ű", ��));
    private const string cloudAnchorKey = "CLOUD_ANCHOR_ID";
    // Ŭ���� ��Ŀ ID
    private string strCloudAnchorId;


    void Start()
    {
        // ��ư �̺�Ʈ ����
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


        // ���� ��Ŀ�� �����ϴ��� ���θ� Ȯ��
        if (localAnchor == null)
        {
            // Raycast �߻�
            if (raycastManager.Raycast(touch.position, hits, TrackableType.PlaneWithinPolygon))
            {
                // ���� ��Ŀ�� ����
                localAnchor = anchorManager.AddAnchor(hits[0].pose);
                // ���� ��Ŀ ��ġ�� �罿 ������Ű�� ������ ����
                anchorGameObject = Instantiate(anchorPrefab, localAnchor.transform);
            }
        }
    }

    // Ŭ���� ��Ŀ ���
    void HostProcessing()
    {
        if (localAnchor == null) return;

        // ��������Ʈ�� ���� �� ����Ƽ ����
        FeatureMapQuality quality = anchorManager.EstimateFeatureMapQualityForHosting(GetCameraPose());

        string mappingText = string.Format("���� ǰ�� = {0}", quality);

        // ���� ����Ƽ�� 1 �̻��� �� ȣ���� ��û
        if (quality == FeatureMapQuality.Sufficient || quality == FeatureMapQuality.Good)
        {
            // 1��¥�� ��Ŀ����Ʈ
            cloudAnchor = anchorManager.HostCloudAnchor(localAnchor, 1);

            if (cloudAnchor == null)
            {
                mappingText = "Ŭ���� ��Ŀ ���� ����";
            }
            else
            {
                // ���⼭ Ŭ���� ��Ŀ ID �� �ȳ���
                // �������� �۾��ϴ� �ð��� �ֱ� ������
                mappingText = "Ŭ���� ��Ŀ ���� ����";
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
            mappingText = $"Ŭ���� ��Ŀ ���� ����, CloudAnchor ID = {cloudAnchor.cloudAnchorId}";
            // ��ĿID ����
            strCloudAnchorId = cloudAnchor.cloudAnchorId;

            // Ŭ���� ��Ŀ ID�� �����ϴ� ����
            // ���� ��Ʈ��ũ : RPC ȣ��
            // ���̾�̽� : ������ ����
            PlayerPrefs.SetString(cloudAnchorKey, strCloudAnchorId);

            // �ʱ�ȭ
            cloudAnchor = null;
            // ������ ������ ��ü�� ����
            Destroy(anchorGameObject);

            mode = Mode.READY;
        }
        else
        {
            mappingText = $"Ŭ���� ��Ŀ ���� ������...{cloudAnchor.cloudAnchorState}";
        }

        messageText.text = mappingText;
    }

    void Resolving()
    {
        if (PlayerPrefs.HasKey(cloudAnchorKey) == false) return;
        messageText.text = "";
        // Ŭ���� ��Ŀ ID �޾ƿ�. (����, ���̾�̽�)
        strCloudAnchorId = PlayerPrefs.GetString(cloudAnchorKey);

        // Ŭ���� ��Ŀ ID�� CloudAnchor �ε�
        cloudAnchor = anchorManager.ResolveCloudAnchorId(strCloudAnchorId);

        if (cloudAnchor == null)
        {
            messageText.text = "Ŭ���� ��Ŀ ������ ����";
        }
        else
        {
            messageText.text = $"Ŭ���� ��Ŀ ������ ���� : {cloudAnchor.cloudAnchorId}";
            // id�� ������ ��
            // ��������Ʈ�� �ļ����� ������
            mode = Mode.RESOLVE_PENDING;
        }
    }

    void ResolvePending()
    {
        if (cloudAnchor.cloudAnchorState == CloudAnchorState.Success)
        {
            messageText.text = "������ ����";

            // ��ü ����
            anchorGameObject = Instantiate(anchorPrefab, cloudAnchor.transform);
            mode = Mode.READY;
        }
        else
        {
            messageText.text = $"������ ���� ��...{cloudAnchor.cloudAnchorState}";
        }
    }


    // MainCamera �±׷� ������ ī�޶��� ��ġ�� ������ Pose ������ Ÿ������ ��ȯ
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
        messageText.text = "�غ�Ϸ�";
        mode = Mode.READY;
    }
}