using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Api.Controllers.Models;
using Api.Database.Context;
using Api.Database.Models;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services
{
    public interface IInspectionAreaService
    {
        public Task<IEnumerable<InspectionArea>> ReadAll(bool readOnly = true);

        public Task<InspectionArea?> ReadById(string id, bool readOnly = true);

        public Task<IEnumerable<InspectionArea>> ReadByInstallation(
            string installationCode,
            bool readOnly = true
        );

        public Task<InspectionArea?> ReadByInstallationAndName(
            string installationCode,
            string inspectionAreaName,
            bool readOnly = true
        );

        public Task<InspectionArea?> ReadByInstallationAndPlantAndName(
            Installation installation,
            Plant plant,
            string inspectionAreaName,
            bool readOnly = true
        );

        public Task<InspectionArea> UpdateAreaPolygon(
            InspectionArea inspectionArea,
            string areaPolygonJson
        );

        public bool MissionTasksAreInsideInspectionAreaPolygon(
            List<MissionTask> missionTasks,
            InspectionArea inspectionArea
        );

        public Task<InspectionArea> Create(CreateInspectionAreaQuery newInspectionArea);

        public Task<InspectionArea> Update(InspectionArea inspectionArea);

        public Task<InspectionArea?> Delete(string id);

        public void DetachTracking(FlotillaDbContext context, InspectionArea inspectionArea);
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
    public class InspectionAreaService(
        FlotillaDbContext context,
        IInstallationService installationService,
        IPlantService plantService,
        IAccessRoleService accessRoleService,
        ISignalRService signalRService
    ) : IInspectionAreaService
    {
        public async Task<IEnumerable<InspectionArea>> ReadAll(bool readOnly = true)
        {
            return await GetInspectionAreas(readOnly: readOnly).ToListAsync();
        }

        public async Task<InspectionArea?> ReadById(string id, bool readOnly = true)
        {
            return await GetInspectionAreas(readOnly: readOnly)
                .FirstOrDefaultAsync(a => a.Id.Equals(id));
        }

        public async Task<IEnumerable<InspectionArea>> ReadByInstallation(
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
            return await GetInspectionAreas(readOnly: readOnly)
                .Where(a => a.Installation != null && a.Installation.Id.Equals(installation.Id))
                .ToListAsync();
        }

        public async Task<InspectionArea?> ReadByInstallationAndName(
            string installationCode,
            string inspectionAreaName,
            bool readOnly = true
        )
        {
            if (inspectionAreaName == null)
            {
                return null;
            }
            return await GetInspectionAreas(readOnly: readOnly)
                .Where(a =>
                    a.Installation != null
                    && a.Installation.InstallationCode.ToLower().Equals(installationCode.ToLower())
                    && a.Name.ToLower().Equals(inspectionAreaName.ToLower())
                )
                .FirstOrDefaultAsync();
        }

        public async Task<InspectionArea?> ReadByInstallationAndPlantAndName(
            Installation installation,
            Plant plant,
            string name,
            bool readOnly = true
        )
        {
            return await GetInspectionAreas(readOnly: readOnly)
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

        public Task<InspectionArea> UpdateAreaPolygon(
            InspectionArea inspectionArea,
            string areaPolygonJson
        )
        {
            inspectionArea.AreaPolygonJson = areaPolygonJson;
            return Update(inspectionArea);
        }

        public bool MissionTasksAreInsideInspectionAreaPolygon(
            List<MissionTask> missionTasks,
            InspectionArea inspectionArea
        )
        {
            if (string.IsNullOrEmpty(inspectionArea.AreaPolygonJson))
            {
                return false;
            }

            var inspectionAreaPolygon = JsonSerializer.Deserialize<InspectionAreaPolygon>(
                inspectionArea.AreaPolygonJson
            );

            var inspectionAreaPositions = inspectionAreaPolygon.Positions;
            var inspectionAreaZMin = inspectionAreaPolygon.ZMin;
            var inspectionAreaZMax = inspectionAreaPolygon.ZMax;
            foreach (var missionTask in missionTasks)
            {
                var robotPosition = missionTask.RobotPose.Position;
                if (
                    !IsPositionInsidePolygon(
                        inspectionAreaPositions,
                        robotPosition,
                        inspectionAreaZMin,
                        inspectionAreaZMax
                    )
                )
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsPositionInsidePolygon(
            List<XYPosition> polygon,
            Position position,
            double zMin,
            double zMax
        )
        {
            var x = position.X;
            var y = position.Y;
            var z = position.Z;

            if (z < zMin || z > zMax)
            {
                return false;
            }

            // Ray-casting algorithm for checking if the point is inside the polygon
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var xi = polygon[i].X;
                var yi = polygon[i].Y;
                var xj = polygon[j].X;
                var yj = polygon[j].Y;

                var intersect =
                    ((yi > y) != (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi);
                if (intersect)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        // public static bool IsPointInPolygon4(
        //     List<XYPosition> polygon,
        //     Position position,
        //     double zMin,
        //     double zMax
        // )
        // {
        //     var x = position.X;
        //     var y = position.Y;
        //     var z = position.Z;

        //     if (z < zMin || z > zMax)
        //     {
        //         return false;
        //     }

        //     bool result = false;
        //     int j = polygon.Count - 1;
        //     for (int i = 0; i < polygon.Count; i++)
        //     {
        //         if (polygon[i].Y < y && polygon[j].Y >= y || polygon[j].Y < y && polygon[i].Y >= y)
        //         {
        //             if (
        //                 polygon[i].X
        //                     + (y - polygon[i].Y)
        //                         / (polygon[j].Y - polygon[i].Y)
        //                         * (polygon[j].X - polygon[i].X)
        //                 < x
        //             )
        //             {
        //                 result = !result;
        //             }
        //         }
        //         j = i;
        //     }
        //     return result;
        // }

        public async Task<InspectionArea> Create(CreateInspectionAreaQuery newInspectionAreaQuery)
        {
            var installation =
                await installationService.ReadByInstallationCode(
                    newInspectionAreaQuery.InstallationCode,
                    readOnly: true
                )
                ?? throw new InstallationNotFoundException(
                    $"No installation with name {newInspectionAreaQuery.InstallationCode} could be found"
                );
            var plant =
                await plantService.ReadByInstallationAndPlantCode(
                    installation,
                    newInspectionAreaQuery.PlantCode,
                    readOnly: true
                )
                ?? throw new PlantNotFoundException(
                    $"No plant with name {newInspectionAreaQuery.PlantCode} could be found"
                );
            var existingInspectionArea = await ReadByInstallationAndPlantAndName(
                installation,
                plant,
                newInspectionAreaQuery.Name,
                readOnly: true
            );

            if (existingInspectionArea != null)
            {
                throw new InspectionAreaExistsException(
                    $"Inspection are with name {newInspectionAreaQuery.Name} already exists"
                );
            }

            var inspectionArea = new InspectionArea
            {
                Name = newInspectionAreaQuery.Name,
                Installation = installation,
                Plant = plant,
            };

            context.Entry(inspectionArea.Installation).State = EntityState.Unchanged;
            context.Entry(inspectionArea.Plant).State = EntityState.Unchanged;

            await context.InspectionAreas.AddAsync(inspectionArea);
            await ApplyDatabaseUpdate(inspectionArea.Installation);
            _ = signalRService.SendMessageAsync(
                "InspectionArea created",
                inspectionArea.Installation,
                new InspectionAreaResponse(inspectionArea)
            );
            DetachTracking(context, inspectionArea);
            return inspectionArea!;
        }

        public async Task<InspectionArea> Update(InspectionArea inspectionArea)
        {
            var entry = context.Update(inspectionArea);
            await ApplyDatabaseUpdate(inspectionArea.Installation);
            _ = signalRService.SendMessageAsync(
                "InspectionArea updated",
                inspectionArea.Installation,
                new InspectionAreaResponse(inspectionArea)
            );
            DetachTracking(context, inspectionArea);
            return entry.Entity;
        }

        public async Task<InspectionArea?> Delete(string id)
        {
            var inspectionArea = await GetInspectionAreas()
                .FirstOrDefaultAsync(ev => ev.Id.Equals(id));
            if (inspectionArea is null)
            {
                return null;
            }

            context.InspectionAreas.Remove(inspectionArea);
            await ApplyDatabaseUpdate(inspectionArea.Installation);
            _ = signalRService.SendMessageAsync(
                "InspectionArea deleted",
                inspectionArea.Installation,
                new InspectionAreaResponse(inspectionArea)
            );

            return inspectionArea;
        }

        private IQueryable<InspectionArea> GetInspectionAreas(bool readOnly = true)
        {
            var accessibleInstallationCodes = accessRoleService.GetAllowedInstallationCodes();
            var query = context
                .InspectionAreas.Include(p => p.Plant)
                .ThenInclude(p => p.Installation)
                .Include(i => i.Installation)
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
                    $"User does not have permission to update inspection area in installation {installation.Name}"
                );
        }

        public void DetachTracking(FlotillaDbContext context, InspectionArea inspectionArea)
        {
            if (
                inspectionArea.Installation != null
                && context.Entry(inspectionArea.Installation).State != EntityState.Detached
            )
                installationService.DetachTracking(context, inspectionArea.Installation);
            if (
                inspectionArea.Plant != null
                && context.Entry(inspectionArea.Plant).State != EntityState.Detached
            )
                plantService.DetachTracking(context, inspectionArea.Plant);
            context.Entry(inspectionArea).State = EntityState.Detached;
        }
    }

    public struct InspectionAreaPolygon
    {
        public double ZMin { get; set; }
        public double ZMax { get; set; }
        public List<XYPosition> Positions { get; set; }
    }

    public struct XYPosition
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
}
