using UnityEngine;

/// <summary>
/// Manos en primera persona. Usa el modelo importado de brazos cuando esta
/// asignado y mantiene los brazos procedurales como respaldo. Al cambiar de
/// arma ajusta la pose y durante la recarga mueve la mano izquierda y el arma.
/// </summary>
public class FirstPersonArms : MonoBehaviour
{
    [Header("Brazos procedurales (respaldo)")]
    public Transform rightForearm;
    public Transform rightHand;
    public Transform leftForearm;
    public Transform leftHand;
    public GameObject magazineProp;

    [Header("Modelo de manos importado")]
    public Transform handsRoot;
    public Transform leftArmBone;
    public Transform leftElbowBone;
    public Transform leftWristBone;
    public Transform rightArmBone;
    public Transform rightElbowBone;
    public Transform rightWristBone;
    public Transform[] leftFingerBones;
    public Transform[] rightFingerBones;

    [Header("Pose del modelo con cada arma")]
    public Vector3 rifleHandsPosition = new Vector3(0.30f, -0.30f, 0.20f);
    public Vector3 rifleHandsEuler = Vector3.zero;
    public float rifleHandsScale = 1f;
    public Vector3 pistolHandsPosition = new Vector3(0.30f, -0.30f, 0.18f);
    public Vector3 pistolHandsEuler = Vector3.zero;
    public float pistolHandsScale = 0.96f;

    [Header("Referencia")]
    public Weapon weapon;

    // Pose con el fusil (coordenadas locales del WeaponPivot).
    private static readonly Vector3 RifleRightHand = new Vector3(0.34f, -0.35f, 0.56f);
    private static readonly Vector3 RifleRightElbow = new Vector3(0.56f, -0.78f, 0.16f);
    private static readonly Vector3 RifleLeftHand = new Vector3(0.16f, -0.28f, 0.78f);
    private static readonly Vector3 RifleLeftElbow = new Vector3(-0.12f, -0.74f, 0.30f);
    private static readonly Vector3 RifleMagHome = new Vector3(0.30f, -0.44f, 0.58f);

    // Pose con la pistola: el brazo izquierdo queda mas abierto y mas bajo.
    private static readonly Vector3 PistolRightHand = new Vector3(0.32f, -0.35f, 0.56f);
    private static readonly Vector3 PistolRightElbow = new Vector3(0.54f, -0.78f, 0.14f);
    private static readonly Vector3 PistolLeftHand = new Vector3(0.10f, -0.60f, 0.44f);
    private static readonly Vector3 PistolLeftElbow = new Vector3(-0.20f, -0.86f, 0.12f);
    private static readonly Vector3 PistolMagHome = new Vector3(0.31f, -0.45f, 0.57f);

    // Puntos alcanzados durante la recarga.
    private static readonly Vector3 ReloadLeftHand = new Vector3(0.30f, -0.50f, 0.58f);
    private static readonly Vector3 ReloadRightHand = new Vector3(0.40f, -0.44f, 0.48f);
    private static readonly Vector3 ReloadLeftElbow = new Vector3(-0.04f, -0.76f, 0.34f);
    private static readonly Vector3 ReloadRightElbow = new Vector3(0.62f, -0.84f, 0.10f);

    private struct ArmPose
    {
        public Vector3 rightHand, rightElbow;
        public Vector3 leftHand, leftElbow;
        public Vector3 magHome;
    }

    private float _weaponBlend;
    private Vector3 _leftArmBasePos, _leftElbowBasePos, _leftWristBasePos;
    private Vector3 _rightArmBasePos, _rightElbowBasePos, _rightWristBasePos;
    private Quaternion _leftArmBaseRot, _leftElbowBaseRot, _leftWristBaseRot;
    private Quaternion _rightArmBaseRot, _rightElbowBaseRot, _rightWristBaseRot;
    private Quaternion[] _leftFingerBaseRot;
    private Quaternion[] _rightFingerBaseRot;

    private void Awake()
    {
        if (weapon == null) weapon = GetComponentInParent<Weapon>();
        if (weapon == null) weapon = Weapon.Instance;
        _weaponBlend = weapon != null ? weapon.CurrentIndex : 0f;
        RememberBone(handsRoot != null ? handsRoot : transform, leftArmBone, ref _leftArmBasePos, ref _leftArmBaseRot);
        RememberBone(handsRoot != null ? handsRoot : transform, leftElbowBone, ref _leftElbowBasePos, ref _leftElbowBaseRot);
        RememberBone(handsRoot != null ? handsRoot : transform, leftWristBone, ref _leftWristBasePos, ref _leftWristBaseRot);
        RememberBone(handsRoot != null ? handsRoot : transform, rightArmBone, ref _rightArmBasePos, ref _rightArmBaseRot);
        RememberBone(handsRoot != null ? handsRoot : transform, rightElbowBone, ref _rightElbowBasePos, ref _rightElbowBaseRot);
        RememberBone(handsRoot != null ? handsRoot : transform, rightWristBone, ref _rightWristBasePos, ref _rightWristBaseRot);

        if (leftFingerBones == null || leftFingerBones.Length == 0)
            leftFingerBones = CollectFingerBones(leftWristBone);
        if (rightFingerBones == null || rightFingerBones.Length == 0)
            rightFingerBones = CollectFingerBones(rightWristBone);
        _leftFingerBaseRot = RememberRotations(leftFingerBones);
        _rightFingerBaseRot = RememberRotations(rightFingerBones);
    }

    private void Update()
    {
        if (weapon == null) return;

        _weaponBlend = Mathf.MoveTowards(_weaponBlend, weapon.CurrentIndex, Time.deltaTime * 7f);

        float reach = weapon.ReloadReach;
        float pull = weapon.ReloadPull;
        float insert = weapon.ReloadInsert;

        if (handsRoot != null) UpdateModel(reach, pull, insert);
        else UpdateProcedural(reach, pull, insert);
        AnimateFingers(reach, pull, insert);

        ArmPose pose = GetPose(_weaponBlend);
        AnimateMagazine(pose.magHome, reach, pull, insert);
    }

    private void UpdateModel(float reach, float pull, float insert)
    {
        handsRoot.localPosition = Vector3.Lerp(rifleHandsPosition, pistolHandsPosition, _weaponBlend);
        handsRoot.localRotation = Quaternion.Euler(Vector3.Lerp(rifleHandsEuler, pistolHandsEuler, _weaponBlend));
        handsRoot.localScale = Vector3.one * Mathf.Lerp(rifleHandsScale, pistolHandsScale, _weaponBlend);

        // La mano izquierda busca el cargador, lo extrae y lo vuelve a colocar.
        SetBone(leftArmBone, _leftArmBasePos, _leftArmBaseRot,
            new Vector3(6f * reach + 3f * pull, 0f, -7f * reach - 2f * pull),
            new Vector3(0f, 0f, 0.012f * reach));
        SetBone(leftElbowBone, _leftElbowBasePos, _leftElbowBaseRot,
            new Vector3(-10f * reach - 4f * pull, 0f, 8f * reach + 3f * pull),
            new Vector3(0f, 0f, 0.018f * reach));
        SetBone(leftWristBone, _leftWristBasePos, _leftWristBaseRot,
            new Vector3(-12f * reach - 5f * pull, 0f, 14f * reach + 8f * pull - 4f * insert),
            new Vector3(-10f * pull, 0f, -0.015f * reach + 0.01f * insert));

        // La derecha sostiene el arma y acompana el giro durante la extraccion.
        SetBone(rightArmBone, _rightArmBasePos, _rightArmBaseRot,
            new Vector3(2f * reach, 0f, -3f * reach - 2f * pull), Vector3.zero);
        SetBone(rightElbowBone, _rightElbowBasePos, _rightElbowBaseRot,
            new Vector3(-4f * reach, 0f, 4f * reach + 2f * pull), Vector3.zero);
        SetBone(rightWristBone, _rightWristBasePos, _rightWristBaseRot,
            new Vector3(-6f * reach, 0f, -10f * reach - 6f * pull + 4f * insert), Vector3.zero);
    }

    private void UpdateProcedural(float reach, float pull, float insert)
    {
        ArmPose pose = GetPose(_weaponBlend);

        Vector3 rh = Vector3.Lerp(pose.rightHand, ReloadRightHand, reach * 0.55f);
        Vector3 re = Vector3.Lerp(pose.rightElbow, ReloadRightElbow, reach * 0.55f);
        Vector3 lh = Vector3.Lerp(pose.leftHand, ReloadLeftHand, reach);
        Vector3 le = Vector3.Lerp(pose.leftElbow, ReloadLeftElbow, reach);
        Vector3 magPull = new Vector3(0.06f, -0.12f, -0.04f) * pull;
        lh += magPull;
        le += magPull * 0.4f;

        ApplyLimb(rightForearm, re, rh, 0.052f);
        ApplyLimb(leftForearm, le, lh, 0.048f);

        if (rightHand != null)
        {
            rightHand.localPosition = rh;
            rightHand.localRotation = Quaternion.Euler(0f, 0f, -12f - 20f * reach - 6f * pull);
        }
        if (leftHand != null)
        {
            leftHand.localPosition = lh;
            leftHand.localRotation = Quaternion.Euler(0f, 0f, 16f + 34f * reach + 18f * pull - 10f * insert);
        }
    }

    private static void RememberBone(Transform root, Transform bone,
        ref Vector3 basePosition, ref Quaternion baseRotation)
    {
        if (bone == null || bone == root) return;
        basePosition = bone.localPosition;
        baseRotation = bone.localRotation;
    }

    private static Transform[] CollectFingerBones(Transform wrist)
    {
        if (wrist == null) return new Transform[0];
        System.Collections.Generic.List<Transform> found = new System.Collections.Generic.List<Transform>();
        string[] keys = { "finger", "thumb", "index", "middle", "ring", "pinky" };
        foreach (Transform bone in wrist.GetComponentsInChildren<Transform>(true))
        {
            if (bone == wrist) continue;
            string name = bone.name.ToLowerInvariant();
            foreach (string key in keys)
            {
                if (name.Contains(key)) { found.Add(bone); break; }
            }
        }
        return found.ToArray();
    }

    private static Quaternion[] RememberRotations(Transform[] bones)
    {
        Quaternion[] rotations = new Quaternion[bones != null ? bones.Length : 0];
        for (int i = 0; i < rotations.Length; i++)
            rotations[i] = bones[i] != null ? bones[i].localRotation : Quaternion.identity;
        return rotations;
    }

    private void AnimateFingers(float reach, float pull, float insert)
    {
        float leftCurl = 18f * reach + 30f * pull - 22f * insert;
        float rightCurl = 10f + 8f * reach - 6f * insert;
        ApplyFingerCurl(leftFingerBones, _leftFingerBaseRot, leftCurl);
        ApplyFingerCurl(rightFingerBones, _rightFingerBaseRot, rightCurl);
    }

    private static void ApplyFingerCurl(Transform[] bones, Quaternion[] baseRotations, float degrees)
    {
        if (bones == null || baseRotations == null) return;
        for (int i = 0; i < bones.Length && i < baseRotations.Length; i++)
        {
            if (bones[i] == null) continue;
            bones[i].localRotation = baseRotations[i] * Quaternion.Euler(degrees, 0f, 0f);
        }
    }

    private static void SetBone(Transform bone, Vector3 basePosition, Quaternion baseRotation,
        Vector3 eulerOffset, Vector3 positionOffset)
    {
        if (bone == null) return;
        bone.localPosition = basePosition + positionOffset;
        bone.localRotation = baseRotation * Quaternion.Euler(eulerOffset);
    }

    private static ArmPose GetPose(float blend)
    {
        return new ArmPose
        {
            rightHand = Vector3.Lerp(RifleRightHand, PistolRightHand, blend),
            rightElbow = Vector3.Lerp(RifleRightElbow, PistolRightElbow, blend),
            leftHand = Vector3.Lerp(RifleLeftHand, PistolLeftHand, blend),
            leftElbow = Vector3.Lerp(RifleLeftElbow, PistolLeftElbow, blend),
            magHome = Vector3.Lerp(RifleMagHome, PistolMagHome, blend)
        };
    }

    private void AnimateMagazine(Vector3 home, float reach, float pull, float insert)
    {
        if (magazineProp == null) return;

        bool visible = weapon.IsReloading && (reach > 0.01f || pull > 0.01f || insert > 0.01f);
        magazineProp.SetActive(visible);
        if (!visible) return;

        // Sale del arma con la mano izquierda, se muestra un instante y se inserta.
        Vector3 outOffset = new Vector3(0.07f, -0.34f, -0.10f);
        Vector3 offset = outOffset * pull;
        offset = Vector3.Lerp(offset, new Vector3(0.01f, -0.03f, 0f), insert);

        magazineProp.transform.localPosition = home + offset;
        magazineProp.transform.localRotation = Quaternion.Euler(
            0f, 12f * pull, 38f * pull * (1f - insert) + 8f * reach);
    }

    /// <summary>Coloca una capsula entre el codo y la mano (el primitivo mide 2 en Y).</summary>
    private static void ApplyLimb(Transform limb, Vector3 elbow, Vector3 hand, float radius)
    {
        if (limb == null) return;
        Vector3 delta = hand - elbow;
        float length = Mathf.Max(0.05f, delta.magnitude);
        limb.localPosition = (elbow + hand) * 0.5f;
        limb.localRotation = Quaternion.FromToRotation(Vector3.up, delta / length);
        limb.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
    }
}
