﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using AutoMapper;
using Dalamud.Logging;
using Dapper;
using FluentDapperLite.Repository;
using PlayerTrack.Models;

namespace PlayerTrack.Infrastructure;

public class ArchiveRecordRepository : BaseRepository
{
    public ArchiveRecordRepository(IDbConnection connection, IMapper mapper)
        : base(connection, mapper)
    {
    }

    public bool CreateArchiveRecord(ArchiveRecord archiveRecord)
    {
        PluginLog.LogVerbose("Entering ArchiveRecordRepository.CreateArchiveRecord()");
        using var transaction = this.Connection.BeginTransaction();
        try
        {
            var migrationArchiveDTO = this.Mapper.Map<ArchiveRecordDTO>(archiveRecord);
            SetCreateTimestamp(migrationArchiveDTO);

            const string sql =
                "INSERT INTO archive_records (archive_type, data, created, updated) " +
                "VALUES (@archive_type, @data, @created, @updated)";
            this.Connection.Execute(sql, migrationArchiveDTO, transaction);

            transaction.Commit();
            return true;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            PluginLog.LogError(ex, "Failed to create new migration archive.");
            return false;
        }
    }

    public bool CreateArchiveRecords(IEnumerable<ArchiveRecord> migrationArchiveRecords)
    {
        PluginLog.LogVerbose("Entering ArchiveRecordRepository.CreateArchiveRecords()");
        using var transaction = this.Connection.BeginTransaction();
        try
        {
            const string sql = @"
            INSERT INTO archive_records (
                archive_type,
                data,
                created,
                updated)
            VALUES (
                @archive_type,
                @data,
                @created,
                @updated)";

            var migrationArchiveDTOs = migrationArchiveRecords
                .Select(this.Mapper.Map<ArchiveRecordDTO>)
                .ToList();

            foreach (var dto in migrationArchiveDTOs)
            {
                SetCreateTimestamp(dto);
            }

            this.Connection.Execute(sql, migrationArchiveDTOs, transaction);

            transaction.Commit();
            return true;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            PluginLog.LogError(ex, "Failed to create new migration archives.");
            return false;
        }
    }
}
