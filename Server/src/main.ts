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
    initializer.registerRpc("rift_window", riftWindowRpc);
    initializer.registerRpc("rift_board", riftBoardRpc);
    initializer.registerRpc("mail_list", mailListRpc);
    initializer.registerRpc("ghost_upload", ghostUploadRpc);
    initializer.registerRpc("ghost_pick", ghostPickRpc);
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
