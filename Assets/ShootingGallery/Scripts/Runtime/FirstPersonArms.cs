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

    // Respalo si el prefab no trae puntos de agarre configurados.
    private static readonly Vector3 RifleRightHand = new Vector3(0.34f, -0.35f, 0.56f);
    private static readonly Vector3 RifleLeftHand = new Vector3(0.16f, -0.28f, 0.78f);

    private struct ArmPose
    {
        public Vector3 rightHand, rightElbow;
        public Vector3 leftHand, leftElbow;
        public Vector3 magHome;
    }

    private Vector3 _rightGrip, _leftGrip;
    private float _pistolBlend;
    private Vector3 _leftArmBasePos, _leftElbowBasePos, _leftWristBasePos;
    private Vector3 _rightArmBasePos, _rightElbowBasePos, _rightWristBasePos;
    private Quaternion _leftArmBaseRot, _leftElbowBaseRot, _leftWristBaseRot;
    private Quaternion _rightArmBaseRot, _rightElbowBaseRot, _rightWristBaseRot;
    private Quaternion[] _leftFingerBaseRot;
    private Quaternion[] _rightFingerBaseRot;
    private Transform _pivot;
    private Vector3 _leftWristRestInRoot, _rightWristRestInRoot;

    private void Awake()
    {
        if (weapon == null) weapon = GetComponentInParent<Weapon>();
        if (weapon == null) weapon = Weapon.Instance;
        WeaponSlot startSlot = weapon != null ? weapon.CurrentSlot : null;
        _rightGrip = startSlot != null ? startSlot.gripRight : RifleRightHand;
        _leftGrip = startSlot != null ? startSlot.gripLeft : RifleLeftHand;
        _pistolBlend = startSlot != null && startSlot.pistol ? 1f : 0f;
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

        if (handsRoot != null)
        {
            _pivot = weapon != null ? weapon.weaponPivot : handsRoot.parent;
            if (_pivot == null) _pivot = handsRoot.parent;
            // Muñecas en reposo medidas desde handsRoot: asi el desplazamiento
            // hacia el arma es siempre el mismo aunque cambie la pose del arma.
            _leftWristRestInRoot = handsRoot.InverseTransformPoint(
                leftWristBone != null ? leftWristBone.position : handsRoot.position);
            _rightWristRestInRoot = handsRoot.InverseTransformPoint(
                rightWristBone != null ? rightWristBone.position : handsRoot.position);
        }
    }

    private void Update()
    {
        if (weapon == null) return;

        WeaponSlot slot = weapon.CurrentSlot;
        if (slot != null)
        {
            // La pose se interpola entre los puntos de agarre del arma anterior
            // y la nueva; ya no depende del indice numerico del arsenal.
            _rightGrip = Vector3.MoveTowards(_rightGrip, slot.gripRight, Time.deltaTime * 4.5f);
            _leftGrip = Vector3.MoveTowards(_leftGrip, slot.gripLeft, Time.deltaTime * 4.5f);
            _pistolBlend = Mathf.MoveTowards(_pistolBlend, slot.pistol ? 1f : 0f, Time.deltaTime * 6f);
        }

        float reach = weapon.ReloadReach;
        float pull = weapon.ReloadPull;
        float insert = weapon.ReloadInsert;

        if (handsRoot != null) UpdateModel(reach, pull, insert);
        else UpdateProcedural(reach, pull, insert);
        AnimateFingers(reach, pull, insert);

        ArmPose pose = GetPose();
        AnimateMagazine(pose.magHome, reach, pull, insert);
    }

    private void UpdateModel(float reach, float pull, float insert)
    {
        handsRoot.localPosition = Vector3.Lerp(rifleHandsPosition, pistolHandsPosition, _pistolBlend);
        handsRoot.localRotation = Quaternion.Euler(Vector3.Lerp(rifleHandsEuler, pistolHandsEuler, _pistolBlend));
        handsRoot.localScale = Vector3.one * Mathf.Lerp(rifleHandsScale, pistolHandsScale, _pistolBlend);

        ArmPose pose = GetPose();

        // Los puntos de agarre son los mismos que usan los brazos procedurales:
        // mano derecha en la empuñadura, izquierda en el guardamanos. Antes el
        // modelo importado se quedaba a 23 cm a cada lado del fusil.
        Vector3 rightTarget = pose.rightHand + new Vector3(
            0.02f * reach, -0.05f * reach - 0.02f * pull, -0.04f * reach - 0.02f * pull);
        Vector3 leftTarget = pose.leftHand + new Vector3(
            0.06f * reach + 0.03f * pull, -0.02f * reach,
            0.02f * reach - 0.03f * pull + 0.02f * insert);

        MoveArm(rightArmBone, rightElbowBone, rightWristBone,
            _rightWristRestInRoot, rightTarget);
        MoveArm(leftArmBone, leftElbowBone, leftWristBone,
            _leftWristRestInRoot, leftTarget);

        // Giro suave de las muñecas para que los dedos rodeen el arma.
        if (rightWristBone != null)
            rightWristBone.localRotation = _rightWristBaseRot * Quaternion.Euler(-16f, 0f, -6f);
        if (leftWristBone != null)
            leftWristBone.localRotation = _leftWristBaseRot * Quaternion.Euler(-8f, 0f, 10f);
    }

    /// <summary>
    /// Traslada todo el brazo (hombro, codo y muñeca) el mismo vector para que
    /// la muñeca caiga sobre el punto de agarre sin estirar la malla importada.
    /// </summary>
    private void MoveArm(Transform arm, Transform elbow, Transform wrist,
        Vector3 restInRoot, Vector3 targetPivotLocal)
    {
        if (_pivot == null || wrist == null || handsRoot == null) return;

        Vector3 restWorld = handsRoot.TransformPoint(restInRoot);
        Vector3 delta = _pivot.TransformPoint(targetPivotLocal) - restWorld;

        // Calcular las tres posiciones antes de asignar: al mover el hombro
        // tambien se mueven sus hijos, y sumar el delta dos veces los romperia.
        Vector3 armTarget = arm != null ? arm.position + delta : Vector3.zero;
        Vector3 elbowTarget = elbow != null ? elbow.position + delta : Vector3.zero;
        Vector3 wristTarget = wrist.position + delta;

        if (arm != null) arm.position = armTarget;
        if (elbow != null) elbow.position = elbowTarget;
        wrist.position = wristTarget;
    }

    private void UpdateProcedural(float reach, float pull, float insert)
    {
        ArmPose pose = GetPose();

        Vector3 rh = Vector3.Lerp(pose.rightHand, pose.rightHand + new Vector3(0.06f, -0.09f, -0.08f), reach * 0.55f);
        Vector3 re = Vector3.Lerp(pose.rightElbow, pose.rightElbow + new Vector3(0.06f, -0.06f, -0.06f), reach * 0.55f);
        Vector3 lh = Vector3.Lerp(pose.leftHand, pose.leftHand + new Vector3(-0.02f, -0.10f, -0.06f), reach);
        Vector3 le = Vector3.Lerp(pose.leftElbow, pose.leftElbow + new Vector3(0.12f, 0.02f, 0.04f), reach);
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
        float leftCurl = 24f * reach + 30f * pull - 22f * insert;
        float rightCurl = 28f + 6f * reach - 8f * insert;
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

    private ArmPose GetPose()
    {
        Vector3 rightElbowOffset = Vector3.Lerp(
            new Vector3(0.22f, -0.43f, -0.40f), new Vector3(0.22f, -0.43f, -0.42f), _pistolBlend);
        Vector3 leftElbowOffset = Vector3.Lerp(
            new Vector3(-0.28f, -0.46f, -0.48f), new Vector3(-0.30f, -0.26f, -0.32f), _pistolBlend);
        Vector3 magOffset = Vector3.Lerp(
            new Vector3(-0.04f, -0.09f, 0.02f), new Vector3(-0.01f, -0.10f, 0.01f), _pistolBlend);

        return new ArmPose
        {
            rightHand = _rightGrip,
            rightElbow = _rightGrip + rightElbowOffset,
            leftHand = _leftGrip,
            leftElbow = _leftGrip + leftElbowOffset,
            magHome = _rightGrip + magOffset
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
