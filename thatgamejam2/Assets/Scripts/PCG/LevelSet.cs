using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레벨 하나(= 리전 하나)에 쓰는 섹션 세트. RunDirector 인스펙터의 Levels 목록에 하나씩 추가.
/// 섹션 목록은 Inspector를 잠그고(자물쇠) 프리팹 여러 개를 리스트 제목 위로 드래그하면 한 번에 들어감.
/// </summary>
[Serializable]
public class LevelSet
{
    public string name = "Easy";

    [Tooltip("Tier가 Start인 섹션들 (하나가 랜덤으로 뽑힘)")]
    public List<SectionInfo> starts = new();
    [Tooltip("Tier가 Goal인 섹션들 (하나가 랜덤으로 뽑힘)")]
    public List<SectionInfo> goals = new();
    [Tooltip("Start와 Goal 사이에 들어갈 섹션 후보들")]
    public List<SectionInfo> sections = new();

    [Tooltip("Start~Goal 사이 섹션 개수 범위")]
    [Min(0)] public int minSections = 3;
    [Min(0)] public int maxSections = 5;

    [Tooltip("이 레벨의 배경 (나중에 BackgroundSwitcher가 사용. 지금은 비워둬도 됨)")]
    public GameObject background;
}