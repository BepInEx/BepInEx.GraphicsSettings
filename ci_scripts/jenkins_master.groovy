lastBuildCommit = ""

pipeline {
    agent any
    stages {
        stage('Pull Projects') {
            steps {
                script {
                    if(currentBuild.previousBuild != null && currentBuild.previousBuild.buildVariables.containsKey("LAST_BUILD"))
                        lastBuildCommit = currentBuild.previousBuild.buildVariables["LAST_BUILD"]
                }
                // Clean up old project before starting
                cleanWs()

                dir('GraphicsSettings') {
                    git 'https://github.com/BepInEx/BepInEx.GraphicsSettings.git'

                    script {
                        longCommit = sh(returnStdout: true, script: "git rev-parse HEAD").trim()
                    }
                }
            }
        }
        stage('Build GraphicsSettings') {
            steps {
                dir('GraphicsSettings') {
                    sh "chmod u+x build.sh"
                    sh "./build.sh --target=Pack --bleeding_edge=true --build_id=${currentBuild.id} --last_build_commit=${lastBuildCommit}"
                }
            }
        }
        stage('Package') {
            steps {
                dir('GraphicsSettings/bin/Release/dist') {
                    archiveArtifacts "*.zip"
                }
            }
        }
    }
    post {
        cleanup {
            script {
                env.LAST_BUILD = lastBuildCommit
            }
        }
        success {
            script {
                lastBuildCommit = longCommit
                dir('GraphicsSettings/bin/Release/dist') {
                    def filesToSend = findFiles(glob: '*.*').collect {it.name}
                    withCredentials([string(credentialsId: 'bepisbuilds_addr', variable: 'BEPISBUILDS_ADDR')]) {
                        sh """curl --upload-file "{${filesToSend.join(',')}}" --ftp-pasv --ftp-skip-pasv-ip --ftp-create-dirs --ftp-method singlecwd --disable-epsv ftp://${BEPISBUILDS_ADDR}/bepinex_graphics_settings/artifacts/${env.BUILD_NUMBER}/"""
                    }
                }
            }

            //Notify Bepin Discord of successfull build
            withCredentials([string(credentialsId: 'discord-notify-webhook', variable: 'DISCORD_WEBHOOK')]) {
                discordSend description: "**Build:** [${currentBuild.id}](${env.BUILD_URL})\n**Status:** [${currentBuild.currentResult}](${env.BUILD_URL})\n\n[**Artifacts on BepisBuilds**](https://builds.bepis.io/projects/bepinex_graphics_settings)", footer: 'Jenkins via Discord Notifier', link: env.BUILD_URL, successful: currentBuild.resultIsBetterOrEqualTo('SUCCESS'), title: "${env.JOB_NAME} #${currentBuild.id}", webhookURL: DISCORD_WEBHOOK
            }
        }
        failure {
            //Notify Discord of failed build
            withCredentials([string(credentialsId: 'discord-notify-webhook', variable: 'DISCORD_WEBHOOK')]) {
                discordSend description: "**Build:** [${currentBuild.id}](${env.BUILD_URL})\n**Status:** [${currentBuild.currentResult}](${env.BUILD_URL})", footer: 'Jenkins via Discord Notifier', link: env.BUILD_URL, successful: currentBuild.resultIsBetterOrEqualTo('SUCCESS'), title: "${env.JOB_NAME} #${currentBuild.id}", webhookURL: DISCORD_WEBHOOK
            }
        }
    }
}