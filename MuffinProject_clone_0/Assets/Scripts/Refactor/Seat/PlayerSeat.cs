using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PlayerSeat : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text hpText;
    
    [SerializeField] private TMP_Text myTurnText;
    
    [SerializeField] private Image myTurnImage;

    [SerializeField] private PlayerPresenter playerPresenter;
    [SerializeField] private Image hpGaugeImage;
    [SerializeField] private TMP_Text cardCountText;

    public int PlayerActorNumber=0;//�����κ�!!

    private float _maxHpGaugeWidth;

    private void Awake()
    {
        gameObject.SetActive(false);
    }
        

    private void OnEnable()
    {
        _maxHpGaugeWidth = hpGaugeImage.rectTransform.rect.width;
    }

    public void SetNicknameUI(string nickname)
    {
        nicknameText.text = nickname;
    }

    public void SetTurnUI(bool isTurn)
    {
        // SetTurnText(isTurn);
        SetTurnImage(isTurn);
    }

    private void SetTurnText(bool isTurn)
    {
        myTurnText.text = isTurn ? "Turn" : "";
    }

    private void SetTurnImage(bool isTurn)
    {
        myTurnImage.enabled = isTurn;
    }
    
    public void SetHpGauge(float currentHp, float maxHp)
    {
        if (maxHp <= 0f)
            return;

        float hpRatio = Mathf.Clamp01(currentHp / maxHp);

        hpGaugeImage.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            _maxHpGaugeWidth * hpRatio
        );
    }
    
    private void SetCardCountUI(int cardCount)
    {
        cardCountText.text = cardCount.ToString();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (TargetSelectionManager.Instance != null && PlayerActorNumber != 0)
        {
            TargetSelectionManager.Instance.ReceiveClick(PlayerActorNumber);
        }
    }


    public void InitPlayerPresenter(int PlayerActorNumber)
    {
        this.PlayerActorNumber = PlayerActorNumber;
        playerPresenter.Init(PlayerActorNumber, GameStatus.Instance.MaxHp);
    }
}
