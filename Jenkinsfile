@Library("telegram-notification@master")

import com.romy.telegram.Telegram

pipeline {
    agent {
        node { 
            label 'docker && kubectl' 
        }
    }
    environment {
        CONFIG = 'config.yaml'
    }

    stages {
        stage('Prepare Pipeline') {
            steps {
                script {
                    project = readYaml file: CONFIG
                }
            }
        }
        stage('Build Image') {
            steps {
                script {
                    imageName = docker.build(
                        "${project.docker.registry.address}/${project.docker.namespace}/${project.docker.image.name}:${project.version}"
                    ).imageName()
                }
            }
        }
        
        stage('Push Image') {
            steps {
                script {
                    docker.withRegistry(
                        "${project.docker.registry.protocol}://${project.docker.registry.address}", 
                        project.docker.registry.credential
                    ) {
                        docker.image(
                            imageName
                        ).push()
                    }
                }
            }
        }

        stage('Deploy Kubernetes') {
            steps {
                script {
                    def kubeconfig = env.getProperty("KUBE_CONFIG_" + project.kubernetes.cluster.toUpperCase())
                    def messages = sh(
                        script: """
                        sed -i 's|image: .*|image: ${imageName}|' ${project.deployment_base_dir}/*.yaml
                        kubectl apply -f ${project.deployment_base_dir} -n ${project.kubernetes.namespace} --kubeconfig ${kubeconfig}
                        """,
                        returnStdout: true
                    ).trim()
                }
            }
        }
    }
}
