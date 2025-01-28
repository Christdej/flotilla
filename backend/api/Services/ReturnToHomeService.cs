using Api.Database.Models;
using Api.Utilities;

namespace Api.Services
{
    public interface IReturnToHomeService
    {
        public Task<MissionRun?> ScheduleReturnToHomeMissionRunIfNotAlreadyScheduled(Robot robot);
        public Task<MissionRun?> GetActiveReturnToHomeMissionRun(
            string robotId,
            bool readOnly = true
        );
    }

    public class ReturnToHomeService(
        ILogger<ReturnToHomeService> logger,
        IMissionRunService missionRunService
    ) : IReturnToHomeService
    {
        public async Task<MissionRun?> ScheduleReturnToHomeMissionRunIfNotAlreadyScheduled(
            Robot robot
        )
        {
            logger.LogInformation(
                "Scheduling return home mission if not already scheduled or the robot is home for robot {RobotId}",
                robot.Id
            );

            if (await IsReturnToHomeMissionAlreadyScheduled(robot.Id))
            {
                logger.LogInformation(
                    "ReturnToHomeMission is already scheduled for Robot {RobotId}",
                    robot.Id
                );
                return null;
            }

            MissionRun missionRun;
            try
            {
                missionRun = await ScheduleReturnToHomeMissionRun(robot);
            }
            catch (Exception ex)
                when (ex
                        is RobotNotFoundException
                            or AreaNotFoundException
                            or InspectionGroupNotFoundException
                            or PoseNotFoundException
                            or UnsupportedRobotCapabilityException
                            or MissionRunNotFoundException
                )
            {
                throw new ReturnToHomeMissionFailedToScheduleException(ex.Message);
            }

            return missionRun;
        }

        private async Task<bool> IsReturnToHomeMissionAlreadyScheduled(string robotId)
        {
            return await missionRunService.PendingOrOngoingReturnToHomeMissionRunExists(robotId);
        }

        private async Task<MissionRun> ScheduleReturnToHomeMissionRun(Robot robot)
        {
            Pose? return_to_home_pose;
            InspectionGroup? currentInspectionGroup;
            if (
                robot.RobotCapabilities is not null
                && robot.RobotCapabilities.Contains(RobotCapabilitiesEnum.auto_return_to_home)
            )
            {
                var previousMissionRun = await missionRunService.ReadLastExecutedMissionRunByRobot(
                    robot.Id,
                    readOnly: true
                );
                currentInspectionGroup = previousMissionRun?.InspectionGroup;
                return_to_home_pose =
                    previousMissionRun?.InspectionGroup?.DefaultLocalizationPose?.Pose == null
                        ? new Pose()
                        : new Pose(previousMissionRun.InspectionGroup.DefaultLocalizationPose.Pose);
            }
            else
            {
                currentInspectionGroup = robot.CurrentInspectionGroup;
                return_to_home_pose =
                    robot.CurrentInspectionGroup?.DefaultLocalizationPose?.Pose == null
                        ? new Pose()
                        : new Pose(robot.CurrentInspectionGroup.DefaultLocalizationPose.Pose);
            }

            if (currentInspectionGroup == null)
            {
                string errorMessage =
                    $"Robot with ID {robot.Id} could return home as it did not have an inspection group";
                logger.LogError("{Message}", errorMessage);
                throw new InspectionGroupNotFoundException(errorMessage);
            }

            var returnToHomeMissionRun = new MissionRun
            {
                Name = "Return home",
                Robot = robot,
                InstallationCode = robot.CurrentInstallation.InstallationCode,
                MissionRunType = MissionRunType.ReturnHome,
                InspectionGroup = currentInspectionGroup!,
                Status = MissionStatus.Pending,
                DesiredStartTime = DateTime.UtcNow,
                Tasks = [new(return_to_home_pose, MissionTaskType.ReturnHome)],
            };

            var missionRun = await missionRunService.Create(returnToHomeMissionRun, false);
            logger.LogInformation(
                "Scheduled a mission for the robot {RobotName} to return to home location on inspection group {InspectionGroupName}",
                robot.Name,
                currentInspectionGroup?.Name
            );
            return missionRun;
        }

        public async Task<MissionRun?> GetActiveReturnToHomeMissionRun(
            string robotId,
            bool readOnly = true
        )
        {
            IList<MissionStatus> activeMissionStatuses =
            [
                MissionStatus.Ongoing,
                MissionStatus.Paused,
            ];
            var activeReturnToHomeMissions = await missionRunService.ReadMissionRuns(
                robotId,
                MissionRunType.ReturnHome,
                activeMissionStatuses,
                readOnly: readOnly
            );

            if (activeReturnToHomeMissions.Count == 0)
            {
                return null;
            }

            if (activeReturnToHomeMissions.Count > 1)
            {
                logger.LogError(
                    "Two Return home missions should not be queued or ongoing simoultaneously for robot with Id {robotId}.",
                    robotId
                );
            }

            return activeReturnToHomeMissions.FirstOrDefault();
        }
    }
}
