This are files for the continuous integration with Travis CI.

On each code push, the following tasks are executed:

* Build the latest code
* Verify the basic neo-cli functionality using [expect](https://linux.die.net/man/1/expect)
* Verify JSON-RPC functionality with curl

The CI integration consists of the following parts:

* `Dockerfile`: the system to build neo-cli and to run the tests
* `build-and-test.sh`: this builds the Docker image, starts it and runs the tests inside. This is useful for testing the CI run on a local dev machine.
* `run-tests-in-docker.sh`: is run inside the Docker container and executes the tests
* `test-neo-cli.expect`: [expect](https://linux.die.net/man/1/expect) script which verifies neo-cli functionality
