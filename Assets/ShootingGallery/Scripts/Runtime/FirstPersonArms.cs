using UnityEngine;

/// <summary>
/// Brazos procedurales en primera persona. Mantiene las manos sobre el arma,
/// cambia la pose al pasar de fusil a pistola y anima la recarga: el arma se
/// inclina, la mano izquierda va al cargador y el cargador cae / vuelve a entrar.
/// </summary>
public class FirstPersonArms : MonoBehaviour
{
    [Header("Mallas de los brazos")]
    public Transform rightForearm;
    public Transform rightHand;
    public Transform leftForearm;
    public Transform leftHand;
    public GameObject magazineProp;

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

    private void Awake()
    {
        if (weapon == null) weapon = GetComponentInParent<Weapon>();
        if (weapon == null) weapon = Weapon.Instance;
        _weaponBlend = weapon != null ? weapon.CurrentIndex : 0f;
    }

    private void Update()
    {
        if (weapon == null) return;

        _weaponBlend = Mathf.MoveTowards(_weaponBlend, weapon.CurrentIndex, Time.deltaTime * 7f);
        ArmPose pose = GetPose(_weaponBlend);

        float reload = weapon.IsReloading ? Mathf.Clamp01(weapon.ReloadProgress) : 0f;
        // Sube, se mantiene mientras se cambia el cargador y baja al terminar.
        float reach = weapon.IsReloading
            ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(reload / 0.35f)) *
              (1f - Mathf.SmoothStep(0.65f, 1f, reload))
            : 0f;

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

        AnimateMagazine(pose.magHome, reload);
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
