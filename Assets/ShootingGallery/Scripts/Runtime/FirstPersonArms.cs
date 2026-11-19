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
    }

    private void Update()
    {
        if (weapon == null) return;

        _weaponBlend = Mathf.MoveTowards(_weaponBlend, weapon.CurrentIndex, Time.deltaTime * 7f);

        float reload = weapon.IsReloading ? Mathf.Clamp01(weapon.ReloadProgress) : 0f;
        float reach = weapon.IsReloading
            ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(reload / 0.35f)) *
              (1f - Mathf.SmoothStep(0.65f, 1f, reload))
            : 0f;

        if (handsRoot != null) UpdateModel(reach);
        else UpdateProcedural(reach);

        ArmPose pose = GetPose(_weaponBlend);
        AnimateMagazine(pose.magHome, reload);
    }

    private void UpdateModel(float reach)
    {
        handsRoot.localPosition = Vector3.Lerp(rifleHandsPosition, pistolHandsPosition, _weaponBlend);
        handsRoot.localRotation = Quaternion.Euler(Vector3.Lerp(rifleHandsEuler, pistolHandsEuler, _weaponBlend));
        handsRoot.localScale = Vector3.one * Mathf.Lerp(rifleHandsScale, pistolHandsScale, _weaponBlend);

        // La mano izquierda va al cargador y la derecha acompana el giro del arma.
        SetBone(leftArmBone, _leftArmBasePos, _leftArmBaseRot,
            new Vector3(6f * reach, 0f, -7f * reach), new Vector3(0f, 0f, 0.012f * reach));
        SetBone(leftElbowBone, _leftElbowBasePos, _leftElbowBaseRot,
            new Vector3(-10f * reach, 0f, 8f * reach), new Vector3(0f, 0f, 0.018f * reach));
        SetBone(leftWristBone, _leftWristBasePos, _leftWristBaseRot,
            new Vector3(-12f * reach, 0f, 14f * reach), new Vector3(0f, 0f, -0.015f * reach));
        SetBone(rightArmBone, _rightArmBasePos, _rightArmBaseRot,
            new Vector3(2f * reach, 0f, -3f * reach), Vector3.zero);
        SetBone(rightElbowBone, _rightElbowBasePos, _rightElbowBaseRot,
            new Vector3(-4f * reach, 0f, 4f * reach), Vector3.zero);
        SetBone(rightWristBone, _rightWristBasePos, _rightWristBaseRot,
            new Vector3(0f, 0f, -10f * reach), Vector3.zero);
    }

    private void UpdateProcedural(float reach)
    {
        ArmPose pose = GetPose(_weaponBlend);

        Vector3 rh = Vector3.Lerp(pose.rightHand, ReloadRightHand, reach * 0.55f);
        Vector3 re = Vector3.Lerp(pose.rightElbow, ReloadRightElbow, reach * 0.55f);
        Vector3 lh = Vector3.Lerp(pose.leftHand, ReloadLeftHand, reach);
        Vector3 le = Vector3.Lerp(pose.leftElbow, ReloadLeftElbow, reach);

        ApplyLimb(rightForearm, re, rh, 0.052f);
        ApplyLimb(leftForearm, le, lh, 0.048f);

        if (rightHand != null)
        {
            rightHand.localPosition = rh;
            rightHand.localRotation = Quaternion.Euler(0f, 0f, -12f - 20f * reach);
        }
        if (leftHand != null)
        {
            leftHand.localPosition = lh;
            leftHand.localRotation = Quaternion.Euler(0f, 0f, 16f + 34f * reach);
        }
    }

    private static void RememberBone(Transform root, Transform bone,
        ref Vector3 basePosition, ref Quaternion baseRotation)
    {
        if (bone == null || bone == root) return;
        basePosition = bone.localPosition;
        baseRotation = bone.localRotation;
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

    private void AnimateMagazine(Vector3 home, float reload)
    {
        if (magazineProp == null) return;

        bool visible = weapon.IsReloading && reload > 0.02f;
        magazineProp.SetActive(visible);
        if (!visible) return;

        float drop = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(reload / 0.45f));
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((reload - 0.55f) / 0.45f));

        Vector3 offset = Vector3.Lerp(Vector3.zero, new Vector3(0.06f, -0.42f, -0.12f), drop);
        offset = Vector3.Lerp(offset, Vector3.zero, rise);

        magazineProp.transform.localPosition = home + offset;
        magazineProp.transform.localRotation = Quaternion.Euler(0f, 0f, 42f * drop * (1f - rise));
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
