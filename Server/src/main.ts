function InitModule(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, initializer: nkruntime.Initializer): void {
    initializer.registerMatch<PartyMatchState>(PartyMatchName, {
        matchInit: partyMatchInit,
        matchJoinAttempt: partyMatchJoinAttempt,
        matchJoin: partyMatchJoin,
        matchLeave: partyMatchLeave,
        matchLoop: partyMatchLoop,
        matchTerminate: partyMatchTerminate,
        matchSignal: partyMatchSignal
    });
    initializer.registerRpc("party_create", partyCreateRpc);
    initializer.registerRpc("party_mine", partyMineRpc);
    initializer.registerRpc("party_connect", partyConnectRpc);
    initializer.registerRpc("party_join", partyJoinRpc);
    initializer.registerRpc("party_leave", partyLeaveRpc);
    initializer.registerRpc("party_kick", partyKickRpc);
    initializer.registerRpc("party_promote", partyPromoteRpc);
    initializer.registerRpc("party_update", partyUpdateRpc);
    initializer.registerRpc("party_list", partyListRpc);
    initializer.registerRpc("party_quick", partyQuickRpc);
    initializer.registerRpc("party_block", partyBlockRpc);
    initializer.registerRpc("party_unblock", partyUnblockRpc);
    initializer.registerRpc("party_report", partyReportRpc);
    initializer.registerRpc("party_chronicle", partyChronicleRpc);
    initializer.registerRpc("clan_create", clanCreateRpc);
    initializer.registerRpc("clan_mine", clanMineRpc);
    initializer.registerRpc("clan_join", clanJoinRpc);
    initializer.registerRpc("clan_leave", clanLeaveRpc);
    initializer.registerRpc("clan_kick", clanKickRpc);
    initializer.registerRpc("clan_promote", clanPromoteRpc);
    initializer.registerRpc("clan_demote", clanDemoteRpc);
    initializer.registerRpc("clan_lead", clanLeadRpc);
    initializer.registerRpc("clan_update", clanUpdateRpc);
    initializer.registerRpc("clan_list", clanListRpc);
    initializer.registerRpc("clan_quick", clanQuickRpc);
    initializer.registerRpc("raid_push", raidPushRpc);
    initializer.registerRpc("raid_view", raidViewRpc);
    initializer.registerRpc("rift_window", riftWindowRpc);
    initializer.registerRpc("rift_board", riftBoardRpc);
    initializer.registerRpc("mail_list", mailListRpc);
    initializer.registerRpc("ghost_upload", ghostUploadRpc);
    initializer.registerRpc("ghost_pick", ghostPickRpc);
    initializer.registerRpc("ghost_thanks", ghostThanksRpc);
    initializer.registerRpc("coop_wallet", coopWalletRpc);
    initializer.registerRpc("mail_claim", mailClaimRpc);
    initializer.registerRpc("telemetry_push", telemetryPushRpc);
    initializer.registerRpc("error_report_push", errorReportPushRpc);
    initializer.registerRpc("telemetry_summary", telemetrySummaryRpc);
    initializer.registerRpc("error_report_list", errorReportListRpc);
    initializer.registerBeforeCreateGroup(partyBeforeCreateGroup);
    initializer.registerBeforeUpdateGroup(partyBeforeGroupChange);
    initializer.registerBeforeDeleteGroup(partyBeforeGroupChange);
    initializer.registerBeforeJoinGroup(partyBeforeGroupChange);
    initializer.registerBeforeLeaveGroup(partyBeforeGroupChange);
    initializer.registerBeforeAddGroupUsers(partyBeforeGroupChange);
    initializer.registerBeforeBanGroupUsers(partyBeforeGroupChange);
    initializer.registerBeforeKickGroupUsers(partyBeforeGroupChange);
    initializer.registerBeforePromoteGroupUsers(partyBeforeGroupChange);
    initializer.registerBeforeDemoteGroupUsers(partyBeforeGroupChange);
    initializer.registerBeforeListGroups(partyBeforeListGroups);
    logger.info("party module loaded");
}
