﻿using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Security.AccessControl;

namespace Community.Security.AccessControl
{
	internal delegate void SecurityEvent(SecurityEventArg e);

	internal class SecurityEventArg : EventArgs
	{
		public int Parts;
		public IntPtr SecurityDesciptor;

		public SecurityEventArg(IntPtr sd, int parts)
		{
			Parts = parts;
			SecurityDesciptor = sd;
		}
	}

	internal class SecurityInfoImpl : NativeMethods.ISecurityInformation, NativeMethods.ISecurityInformation3, NativeMethods.ISecurityObjectTypeInfo, NativeMethods.IEffectivePermission//, NativeMethods.ISecurityInformation4, NativeMethods.IEffectivePermission2
	{
		internal NativeMethods.SI_OBJECT_INFO ObjectInfo;
		private ObjInfoFlags currentElevation;
		private string fullObjectName;
		private IAccessControlEditorDialogProvider prov;
		private NativeMethods.MarshaledMem pSD;

		public SecurityInfoImpl(ObjInfoFlags flags, string objectName, string fullName, string serverName = null, string pageTitle = null)
		{
			ObjectInfo = new NativeMethods.SI_OBJECT_INFO(flags, objectName, serverName, pageTitle);
			currentElevation = 0; // flags & (ObjInfoFlags.OwnerElevationRequired | ObjInfoFlags.AuditElevationRequired | ObjInfoFlags.PermsElevationRequired);
			fullObjectName = fullName;
		}

		public event SecurityEvent OnSetSecurity;

		public byte[] SecurityDescriptor
		{
			get { return pSD.Array; }
			set { pSD = new NativeMethods.MarshaledMem(value); }
		}

		void NativeMethods.IEffectivePermission.GetEffectivePermission(Guid pguidObjectType, IntPtr pUserSid, string pszServerName, IntPtr pSD, out ObjectTypeList[] ppObjectTypeList, out uint pcObjectTypeListLength, out uint[] ppGrantedAccessList, out uint pcGrantedAccessListLength)
		{
			System.Diagnostics.Debug.WriteLine(string.Format("GetEffectivePermission: {0}, {1}", pguidObjectType, pszServerName));
			if (pguidObjectType == Guid.Empty)
			{
				ppGrantedAccessList = this.prov.GetEffectivePermission(pUserSid, pszServerName, pSD);
				pcGrantedAccessListLength = (uint)ppGrantedAccessList.Length;
				ppObjectTypeList = new ObjectTypeList[] { ObjectTypeList.Self };
				pcObjectTypeListLength = (uint)ppObjectTypeList.Length;
			}
			else
			{
				ppGrantedAccessList = this.prov.GetEffectivePermission(pguidObjectType, pUserSid, pszServerName, pSD, out ppObjectTypeList);
				pcGrantedAccessListLength = (uint)ppGrantedAccessList.Length;
				pcObjectTypeListLength = (uint)ppObjectTypeList.Length;
			}
		}

		/*void NativeMethods.IEffectivePermission2.ComputeEffectivePermissionWithSecondarySecurity(IntPtr pSid, IntPtr pDeviceSid, string pszServerName, NativeMethods.SECURITY_OBJECT[] pSecurityObjects, uint dwSecurityObjectCount, ref NativeMethods.TOKEN_GROUPS pUserGroups, NativeMethods.AuthzSidOperation[] pAuthzUserGroupsOperations, ref NativeMethods.TOKEN_GROUPS pDeviceGroups, NativeMethods.AuthzSidOperation[] pAuthzDeviceGroupsOperations, ref NativeMethods.AUTHZ_SECURITY_ATTRIBUTES_INFORMATION pAuthzUserClaims, ref NativeMethods.AuthzSecurityAttributeOperation pAuthzUserClaimsOperations, ref NativeMethods.AUTHZ_SECURITY_ATTRIBUTES_INFORMATION pAuthzDeviceClaims, ref NativeMethods.AuthzSecurityAttributeOperation pAuthzDeviceClaimsOperations, ref NativeMethods.EFFPERM_RESULT_LIST pEffpermResultLists)
		{
			System.Diagnostics.Debug.WriteLine(string.Format("ComputeEffectivePermissionWithSecondarySecurity: {0}", dwSecurityObjectCount));
			pEffpermResultLists = new NativeMethods.EFFPERM_RESULT_LIST();
		}*/

		void NativeMethods.ISecurityInformation.GetAccessRights(Guid guidObject, int dwFlags, out AccessRightInfo[] access, ref uint access_count, out uint DefaultAccess)
		{
			System.Diagnostics.Debug.WriteLine(string.Format("GetAccessRight: {0}, {1}", guidObject, (ObjInfoFlags)dwFlags));
			uint defAcc;
			AccessRightInfo[] ari;
			this.prov.GetAccessListInfo((ObjInfoFlags)dwFlags, out ari, out defAcc);
			DefaultAccess = defAcc;
			access = ari;
			access_count = (uint)access.Length;
		}

		void NativeMethods.ISecurityInformation.GetInheritTypes(out InheritTypeInfo[] InheritTypes, out uint InheritTypesCount)
		{
			System.Diagnostics.Debug.WriteLine("GetInheritTypes");
			InheritTypes = this.prov.GetInheritTypes();
			InheritTypesCount = (uint)InheritTypes.Length;
		}

		void NativeMethods.ISecurityInformation.GetObjectInformation(ref NativeMethods.SI_OBJECT_INFO object_info)
		{
			System.Diagnostics.Debug.WriteLine(string.Format("GetObjectInformation: {0} {1}", this.ObjectInfo.Flags, this.currentElevation));
			object_info = this.ObjectInfo;
			object_info.Flags &= ~(this.currentElevation);
		}

		void NativeMethods.ISecurityInformation.GetSecurity(int RequestInformation, out IntPtr ppSecurityDescriptor, bool fDefault)
		{
			System.Diagnostics.Debug.WriteLine(string.Format("GetSecurity: {0}{1}", (SecurityInfosEx)RequestInformation, fDefault ? " (Def)" : ""));
			ppSecurityDescriptor = NativeMethods.GetPrivateObjectSecurity(fDefault ? this.prov.GetDefaultSecurity() : pSD.Ptr, RequestInformation);
			System.Diagnostics.Debug.WriteLine(string.Format("GetSecurity={0}<-{1}", NativeMethods.SecurityDescriptorPtrToSDLL(ppSecurityDescriptor, RequestInformation), NativeMethods.SecurityDescriptorPtrToSDLL(pSD.Ptr, RequestInformation)));
		}

		void NativeMethods.ISecurityInformation.MapGeneric(Guid guidObjectType, ref sbyte AceFlags, ref uint Mask)
		{
			uint stMask = Mask;
			GenericMapping gm = this.prov.GetGenericMapping(AceFlags);
			NativeMethods.MapGenericMask(ref Mask, ref gm);
			//if (Mask != gm.GenericAll)
			//	Mask &= ~(uint)FileSystemRights.Synchronize;
			System.Diagnostics.Debug.WriteLine(string.Format("MapGeneric: {0}, {1}, 0x{2:X}->0x{3:X}", guidObjectType, (AceFlags)AceFlags, stMask, Mask));
		}

		void NativeMethods.ISecurityInformation.PropertySheetPageCallback(IntPtr hwnd, PropertySheetCallbackMessage uMsg, SecurityPageType uPage)
		{
			System.Diagnostics.Debug.WriteLine(string.Format("PropertySheetPageCallback: {0}, {1}, {2}", hwnd, uMsg, uPage));
			this.prov.PropertySheetPageCallback(hwnd, uMsg, uPage);
		}

		void NativeMethods.ISecurityInformation.SetSecurity(int RequestInformation, IntPtr sd)
		{
			if (OnSetSecurity != null)
				OnSetSecurity(new SecurityEventArg(sd, RequestInformation));
		}

		void NativeMethods.ISecurityInformation3.GetFullResourceName(out string szResourceName)
		{
			szResourceName = this.fullObjectName;
		}

		void NativeMethods.ISecurityInformation3.OpenElevatedEditor(IntPtr hWnd, uint uPage)
		{
			SecurityPageType pgType = (SecurityPageType)(uPage & 0x0000FFFF);
			SecurityPageActivation pgActv = (SecurityPageActivation)((uPage >> 16) & 0x0000FFFF);
			System.Diagnostics.Debug.WriteLine(string.Format("OpenElevatedEditor: {0} - {1}", pgType, pgActv));
			ObjInfoFlags lastElev = this.currentElevation;
			switch (pgActv)
			{
				case SecurityPageActivation.ShowDefault:
					this.currentElevation |= (ObjInfoFlags.PermsElevationRequired | ObjInfoFlags.ViewOnly);
					break;
				case SecurityPageActivation.ShowPermActivated:
					this.currentElevation |= (ObjInfoFlags.PermsElevationRequired | ObjInfoFlags.ViewOnly);
					pgType = SecurityPageType.AdvancedPermissions;
					break;
				case SecurityPageActivation.ShowAuditActivated:
					this.currentElevation |= ObjInfoFlags.AuditElevationRequired;
					pgType = SecurityPageType.Audit;
					break;
				case SecurityPageActivation.ShowOwnerActivated:
					this.currentElevation |= ObjInfoFlags.OwnerElevationRequired;
					pgType = SecurityPageType.Owner;
					break;
				case SecurityPageActivation.ShowEffectiveActivated:
					break;
				case SecurityPageActivation.ShowShareActivated:
					break;
				case SecurityPageActivation.ShowCentralPolicyActivated:
					break;
				default:
					break;
			}
			this.ShowDialog(hWnd, pgType, pgActv);
			this.currentElevation = lastElev;
		}

		/*void NativeMethods.ISecurityInformation4.GetSecondarySecurity(out NativeMethods.SECURITY_OBJECT[] securityObjects, out uint securityObjectCount)
		{
			System.Diagnostics.Debug.WriteLine(string.Format("GetSecondarySecurity:"));
			securityObjects = new NativeMethods.SECURITY_OBJECT[0];
			securityObjectCount = 0;
		}*/

		void NativeMethods.ISecurityObjectTypeInfo.GetInheritSource(int si, IntPtr pACL, out InheritedFromInfo[] ppInheritArray)
		{
			System.Diagnostics.Debug.WriteLine(string.Format("GetInheritSource: {0}", (SecurityInfosEx)si));
			ppInheritArray = this.prov.GetInheritSource(this.fullObjectName, this.ObjectInfo.ServerName, this.ObjectInfo.IsContainer, (uint)si, pACL);
		}

		public void SetProvider(IAccessControlEditorDialogProvider provider)
		{
			prov = provider;
		}

		public RawSecurityDescriptor ShowDialog(IntPtr hWnd, SecurityPageType pageType = SecurityPageType.BasicPermissions, SecurityPageActivation pageAct = SecurityPageActivation.ShowDefault)
		{
			System.Diagnostics.Debug.WriteLine(string.Format("ShowDialog: {0} {1}", pageType, pageAct));
			SecurityEventArg sd = null;
			SecurityEvent fn = delegate(SecurityEventArg e) { sd = e; };
			try
			{
				this.OnSetSecurity += fn;
				if (System.Environment.OSVersion.Version.Major == 5 || (pageType == SecurityPageType.BasicPermissions && pageAct == SecurityPageActivation.ShowDefault))
				{
					if (!NativeMethods.EditSecurity(hWnd, this))
						Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
				}
				else
				{
					uint uPage = (uint)pageType & ((uint)pageAct << 16);
					NativeMethods.EditSecurityAdvanced(hWnd, this, uPage);
				}
				if (sd != null)
				{
					string sddl = NativeMethods.SecurityDescriptorPtrToSDLL(sd.SecurityDesciptor, sd.Parts);
					if (!string.IsNullOrEmpty(sddl))
					{
						System.Diagnostics.Debug.WriteLine(string.Format("ShowDialog: Return: {0}", sddl));
						return new RawSecurityDescriptor(sddl);
					}
				}
			}
			finally
			{
				this.OnSetSecurity -= fn;
			}
			System.Diagnostics.Debug.WriteLine(string.Format("ShowDialog: Return: null"));
			return null;
		}
	}
}
