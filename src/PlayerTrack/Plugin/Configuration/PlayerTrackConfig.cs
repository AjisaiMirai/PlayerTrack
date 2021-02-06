﻿using System.Collections.Generic;

namespace PlayerTrack
{
	public abstract class PlayerTrackConfig
	{
		public int AlertFrequency = 14400000;
		public int BackupFrequency = 14400000;
		public int BackupRetention = 10;
		public List<FontAwesomeIcon> EnabledIcons = new List<FontAwesomeIcon>();
		public bool IncludeNotesInAlert = false;
		public long LastBackup = 0;
		public int LodestoneCooldownDuration = 3600000;
		public int LodestoneFailureDelay = 86400000;
		public TrackLodestoneLocale LodestoneLocale = TrackLodestoneLocale.na;
		public int LodestoneMaxFailure = 3;
		public int LodestoneMaxRetry = 5;
		public int LodestoneRequestDelay = 30000;
		public int LodestoneTimeout = 60000;
		public int NewEncounterThreshold = 86400000;
		public List<uint> PermittedContent = new List<uint>();
		public int RecentPlayerThreshold = 900000;
		public bool RestrictInCombat = true;
		public bool RestrictToContent = true;
		public bool RestrictToCustom = false;
		public bool RestrictToHighEndDuty = false;
		public int SaveFrequency = 15000;
		public bool SyncToLodestone = true;
		public int UpdateFrequency = 5000;
		public bool FreshInstall { get; set; } = true;
		public bool Compressed { get; set; } = true;
		public int SchemaVersion { get; set; } = 1;
		public bool Enabled { get; set; } = true;
		public int PluginLanguage { get; set; } = 0;
		public bool EnableAlerts { get; set; } = true;
		public int AlertDelay { get; set; } = 1000;
		public bool ShowOverlay { get; set; } = true;
		public bool SetCurrentTargetOnRightClick { get; set; } = true;
		public bool SetFocusTargetOnHover { get; set; } = false;
		public bool ShowPlayerCharacterDetails { get; set; } = false;
		public bool ShowPlayerOverride { get; set; } = false;
	}
}