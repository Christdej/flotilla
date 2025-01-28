using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Api.Controllers.Models;
using Api.Database.Context;
using Api.Database.Models;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services
{
    public interface IInspectionGroupService
    {
        public Task<IEnumerable<InspectionGroup>> ReadAll(bool readOnly = true);

        public Task<InspectionGroup?> ReadById(string id, bool readOnly = true);

        public Task<IEnumerable<InspectionGroup>> ReadByInstallation(
            string installationCode,
            bool readOnly = true
        );

        public Task<InspectionGroup?> ReadByInstallationAndName(
            string installationCode,
            string inspectionGroupName,
            bool readOnly = true
        );

        public Task<InspectionGroup?> ReadByInstallationAndPlantAndName(
            Installation installation,
            Plant plant,
            string inspectionGroupName,
            bool readOnly = true
        );

        public Task<InspectionGroup> Create(CreateInspectionGroupQuery newInspectionGroup);

        public Task<InspectionGroup> Update(InspectionGroup inspectionGroup);

        public Task<InspectionGroup?> Delete(string id);

        public void DetachTracking(FlotillaDbContext context, InspectionGroup inspectionGroup);
    }

    [SuppressMessage(
        "Globalization",
        "CA1309:Use ordinal StringComparison",
        Justification = "EF Core refrains from translating string comparison overloads to SQL"
    )]
    [SuppressMessage(
        "Globalization",
        "CA1304:Specify CultureInfo",
        Justification = "Entity framework does not support translating culture info to SQL calls"
    )]
    public class InspectionGroupService(
        FlotillaDbContext context,
        IDefaultLocalizationPoseService defaultLocalizationPoseService,
        IInstallationService installationService,
        IPlantService plantService,
        IAccessRoleService accessRoleService,
        ISignalRService signalRService
    ) : IInspectionGroupService
    {
        public async Task<IEnumerable<InspectionGroup>> ReadAll(bool readOnly = true)
        {
            return await GetInspectionGroups(readOnly: readOnly).ToListAsync();
        }

        public async Task<InspectionGroup?> ReadById(string id, bool readOnly = true)
        {
            return await GetInspectionGroups(readOnly: readOnly)
                .FirstOrDefaultAsync(a => a.Id.Equals(id));
        }

        public async Task<IEnumerable<InspectionGroup>> ReadByInstallation(
            string installationCode,
            bool readOnly = true
        )
        {
            var installation = await installationService.ReadByInstallationCode(
                installationCode,
                readOnly: true
            );
            if (installation == null)
            {
                return [];
            }
            return await GetInspectionGroups(readOnly: readOnly)
                .Where(a => a.Installation != null && a.Installation.Id.Equals(installation.Id))
                .ToListAsync();
        }

        public async Task<InspectionGroup?> ReadByInstallationAndName(
            string installationCode,
            string inspectionGroupName,
            bool readOnly = true
        )
        {
            if (inspectionGroupName == null)
            {
                return null;
            }
            return await GetInspectionGroups(readOnly: readOnly)
                .Where(a =>
                    a.Installation != null
                    && a.Installation.InstallationCode.ToLower().Equals(installationCode.ToLower())
                    && a.Name.ToLower().Equals(inspectionGroupName.ToLower())
                )
                .FirstOrDefaultAsync();
        }

        public async Task<InspectionGroup?> ReadByInstallationAndPlantAndName(
            Installation installation,
            Plant plant,
            string name,
            bool readOnly = true
        )
        {
            return await GetInspectionGroups(readOnly: readOnly)
                .Where(a =>
                    a.Plant != null
                    && a.Plant.Id.Equals(plant.Id)
                    && a.Installation != null
                    && a.Installation.Id.Equals(installation.Id)
                    && a.Name.ToLower().Equals(name.ToLower())
                )
                .Include(d => d.Plant)
                .Include(i => i.Installation)
                .FirstOrDefaultAsync();
        }

        public async Task<InspectionGroup> Create(
            CreateInspectionGroupQuery newInspectionGroupQuery
        )
        {
            var installation =
                await installationService.ReadByInstallationCode(
                    newInspectionGroupQuery.InstallationCode,
                    readOnly: true
                )
                ?? throw new InstallationNotFoundException(
                    $"No installation with name {newInspectionGroupQuery.InstallationCode} could be found"
                );
            var plant =
                await plantService.ReadByInstallationAndPlantCode(
                    installation,
                    newInspectionGroupQuery.PlantCode,
                    readOnly: true
                )
                ?? throw new PlantNotFoundException(
                    $"No plant with name {newInspectionGroupQuery.PlantCode} could be found"
                );
            var existingInspectionGroup = await ReadByInstallationAndPlantAndName(
                installation,
                plant,
                newInspectionGroupQuery.Name,
                readOnly: true
            );

            if (existingInspectionGroup != null)
            {
                throw new InspectionGroupExistsException(
                    $"Inspection are with name {newInspectionGroupQuery.Name} already exists"
                );
            }

            DefaultLocalizationPose? defaultLocalizationPose = null;
            if (newInspectionGroupQuery.DefaultLocalizationPose != null)
            {
                defaultLocalizationPose = await defaultLocalizationPoseService.Create(
                    new DefaultLocalizationPose(
                        newInspectionGroupQuery.DefaultLocalizationPose.Value.Pose,
                        newInspectionGroupQuery.DefaultLocalizationPose.Value.IsDockingStation
                    )
                );
            }

            var inspectionGroup = new InspectionGroup
            {
                Name = newInspectionGroupQuery.Name,
                Installation = installation,
                Plant = plant,
                DefaultLocalizationPose = defaultLocalizationPose,
            };

            context.Entry(inspectionGroup.Installation).State = EntityState.Unchanged;
            context.Entry(inspectionGroup.Plant).State = EntityState.Unchanged;
            if (inspectionGroup.DefaultLocalizationPose is not null)
            {
                context.Entry(inspectionGroup.DefaultLocalizationPose).State = EntityState.Modified;
            }

            await context.InspectionGroups.AddAsync(inspectionGroup);
            await ApplyDatabaseUpdate(inspectionGroup.Installation);
            _ = signalRService.SendMessageAsync(
                "InspectionGroup created",
                inspectionGroup.Installation,
                new InspectionGroupResponse(inspectionGroup)
            );
            DetachTracking(context, inspectionGroup);
            return inspectionGroup!;
        }

        public async Task<InspectionGroup> Update(InspectionGroup inspectionGroup)
        {
            var entry = context.Update(inspectionGroup);
            await ApplyDatabaseUpdate(inspectionGroup.Installation);
            _ = signalRService.SendMessageAsync(
                "InspectionGroup updated",
                inspectionGroup.Installation,
                new InspectionGroupResponse(inspectionGroup)
            );
            DetachTracking(context, inspectionGroup);
            return entry.Entity;
        }

        public async Task<InspectionGroup?> Delete(string id)
        {
            var inspectionGroup = await GetInspectionGroups()
                .FirstOrDefaultAsync(ev => ev.Id.Equals(id));
            if (inspectionGroup is null)
            {
                return null;
            }

            context.InspectionGroups.Remove(inspectionGroup);
            await ApplyDatabaseUpdate(inspectionGroup.Installation);
            _ = signalRService.SendMessageAsync(
                "InspectionGroup deleted",
                inspectionGroup.Installation,
                new InspectionGroupResponse(inspectionGroup)
            );

            return inspectionGroup;
        }

        private IQueryable<InspectionGroup> GetInspectionGroups(bool readOnly = true)
        {
            var accessibleInstallationCodes = accessRoleService.GetAllowedInstallationCodes();
            var query = context
                .InspectionGroups.Include(p => p.Plant)
                .ThenInclude(p => p.Installation)
                .Include(i => i.Installation)
                .Include(d => d.DefaultLocalizationPose)
                .Where(
                    (d) =>
                        accessibleInstallationCodes.Result.Contains(
                            d.Installation.InstallationCode.ToUpper()
                        )
                );
            return readOnly ? query.AsNoTracking() : query.AsTracking();
        }

        private async Task ApplyDatabaseUpdate(Installation? installation)
        {
            var accessibleInstallationCodes = await accessRoleService.GetAllowedInstallationCodes();
            if (
                installation == null
                || accessibleInstallationCodes.Contains(
                    installation.InstallationCode.ToUpper(CultureInfo.CurrentCulture)
                )
            )
                await context.SaveChangesAsync();
            else
                throw new UnauthorizedAccessException(
                    $"User does not have permission to update inspection Group in installation {installation.Name}"
                );
        }

        public void DetachTracking(FlotillaDbContext context, InspectionGroup inspectionGroup)
        {
            if (
                inspectionGroup.Installation != null
                && context.Entry(inspectionGroup.Installation).State != EntityState.Detached
            )
                installationService.DetachTracking(context, inspectionGroup.Installation);
            if (
                inspectionGroup.Plant != null
                && context.Entry(inspectionGroup.Plant).State != EntityState.Detached
            )
                plantService.DetachTracking(context, inspectionGroup.Plant);
            if (
                inspectionGroup.DefaultLocalizationPose != null
                && context.Entry(inspectionGroup.DefaultLocalizationPose).State
                    != EntityState.Detached
            )
                defaultLocalizationPoseService.DetachTracking(
                    context,
                    inspectionGroup.DefaultLocalizationPose
                );
            context.Entry(inspectionGroup).State = EntityState.Detached;
        }
    }
}
