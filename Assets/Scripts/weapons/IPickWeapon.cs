// anything that can hold a weapon implements this, so a pickup does not need to
// know whether it is arming the player or an enemy
public interface IPickWeapon
{
    void EquipWeapon(WeaponStats weapon, int ammoOverride = -1);
}